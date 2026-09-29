//! Mod loading (M3): scan `<game>/Mods/*.dll`, dlopen each, call its
//! `aniimod_entry`, and hand the mod an [`Engine`] implementation. Panics
//! are caught per-mod so one broken mod can never take the game down.

#![cfg(target_family = "windows")]
#![allow(clippy::missing_safety_doc)]

use std::sync::Arc;
use std::ffi::c_void;
use std::sync::OnceLock;
use std::path::PathBuf;

use aniimod_ffi::Il2Cpp;
use aniimod_mod::{ClassPtr, Engine, FieldPtr, MethodPtr, Mod, NativeFn, SceneCallback};

#[link(name = "kernel32")]
extern "system" {
    fn LoadLibraryW(name: *const u16) -> isize;
    fn GetProcAddress(module: isize, name: *const u8) -> isize;
}

struct EngineImpl {
    mod_name: String,
    il2cpp: *const Il2Cpp,
    images: &'static ImageTable,
}

struct SendPtr<T>(*const T);
unsafe impl<T> Send for SendPtr<T> {}
unsafe impl<T> Sync for SendPtr<T> {}

/// The image table, process-lifetime. FIX (2026-09-27 crash): load_all used
/// to pass `&images.to_vec()` — a reference to a TEMPORARY that dropped at
/// the end of the statement; the poll thread later iterated freed memory
/// (AV inside find_class's name compare). EngineImpl now holds a &'static.
pub(crate) struct ImageTable(pub Vec<(String, aniimod_ffi::Il2CppImage)>);
unsafe impl Send for ImageTable {}
unsafe impl Sync for ImageTable {}
pub(crate) static IMAGES: OnceLock<ImageTable> = OnceLock::new();

// The raw pointers are only dereferenced after the domain is ready; the
// Il2Cpp struct itself is plain function pointers, and the images vec lives
// as long as the process. All poll/detour threads only touch these.
unsafe impl Send for EngineImpl {}
unsafe impl Sync for EngineImpl {}


impl Engine for EngineImpl {
    fn log(&mut self, message: &str) {
        crate::log_line(&format!("[{}] {}", self.mod_name, message));
    }

    fn find_class(&mut self, image: &str, namespace: &str, name: &str) -> Result<ClassPtr, String> {
        let images = unsafe { &*self.images };
        let il2cpp = unsafe { &*self.il2cpp };
        let image_ptr = images
            .0
            .iter()
            .find(|(n, _)| n.eq_ignore_ascii_case(image))
            .map(|(_, p)| *p)
            .ok_or_else(|| format!("image {image:?} not loaded"))?;
        let ns = c_string(namespace);
        let nm = c_string(name);
        let class = unsafe {
            (il2cpp.class_from_name)(
                image_ptr,
                ns.as_ptr() as *const core::ffi::c_char,
                nm.as_ptr() as *const core::ffi::c_char,
            )
        };
        if class.is_null() {
            Err(format!("class {namespace}.{name} not found in {image}"))
        } else {
            Ok(class)
        }
    }

    fn on_scene_changed(&mut self, callback: SceneCallback) -> Result<(), String> {
        crate::events::subscribe_scene(callback);
        Ok(())
    }

    fn find_method(&mut self, class: ClassPtr, name: &str, argc: i32) -> Result<MethodPtr, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        if class.is_null() {
            return Err("null class".into());
        }
        let nm = c_string(name);
        let m = unsafe {
            (il2cpp.class_get_method_from_name)(class, nm.as_ptr() as *const core::ffi::c_char, argc)
        };
        if m.is_null() {
            Err(format!("method {name}/{argc} not found"))
        } else {
            Ok(m)
        }
    }

    fn find_field(&mut self, class: ClassPtr, name: &str) -> Result<FieldPtr, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        if class.is_null() {
            return Err("null class".into());
        }
        let nm = c_string(name);
        let f = unsafe { (il2cpp.class_get_field_from_name)(class, nm.as_ptr() as *const core::ffi::c_char) };
        if f.is_null() {
            Err(format!("field {name} not found"))
        } else {
            Ok(f)
        }
    }

    fn make_string(&mut self, s: &str) -> Result<*mut core::ffi::c_void, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        let cs = c_string(s);
        let p = unsafe { (il2cpp.string_new)(cs.as_ptr() as *const core::ffi::c_char) };
        if p.is_null() {
            Err("il2cpp_string_new returned null".into())
        } else {
            Ok(p as *mut core::ffi::c_void)
        }
    }

    fn call_method(
        &mut self,
        method: MethodPtr,
        obj: *mut core::ffi::c_void,
        args: &[*mut core::ffi::c_void],
    ) -> Result<*mut core::ffi::c_void, String> {
        if method.is_null() {
            return Err("null method".into());
        }
        let invoke = crate::events::original_invoke()
            .ok_or("runtime_invoke original not captured yet")?;
        // runtime_invoke wants void** params: one pointer per arg, each
        // pointing at that argument's value slot.
        // runtime_invoke wants params[i] = POINTER TO the i-th argument's
        // value slot (for object args: pointer to the reference).
        let mut slots: Vec<*mut core::ffi::c_void> = args.to_vec();
        let mut params: Vec<*mut core::ffi::c_void> =
            slots.iter_mut().map(|s| s as *mut _ as *mut core::ffi::c_void).collect();
        let mut exc: *mut core::ffi::c_void = core::ptr::null_mut();
        let result = unsafe {
            invoke(method, obj, params.as_mut_ptr() as *mut core::ffi::c_void, &mut exc as *mut _ as *mut core::ffi::c_void)
        };
        if !exc.is_null() {
            return Err("target method raised an il2cpp exception".into());
        }
        Ok(result)
    }

    fn unbox_f32(&self, boxed: *mut core::ffi::c_void) -> f32 {
        if boxed.is_null() {
            return 0.0;
        }
        unsafe { core::ptr::read_unaligned((boxed as *const u8).add(0x10) as *const f32) }
    }

    fn string_length(&self, s: *mut core::ffi::c_void) -> u32 {
        if s.is_null() {
            return 0;
        }
        unsafe { core::ptr::read_unaligned((s as *const u8).add(0x10) as *const u32) }
    }

    fn read_static_field(&mut self, field: FieldPtr) -> Result<*mut core::ffi::c_void, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        if field.is_null() {
            return Err("null field".into());
        }
        let mut out: *mut core::ffi::c_void = core::ptr::null_mut();
        unsafe { (il2cpp.field_static_get_value)(field, &mut out as *mut _ as *mut core::ffi::c_void) };
        Ok(out)
    }

    fn hook_method(&mut self, method: MethodPtr, detour: NativeFn) -> Result<NativeFn, String> {
        if method.is_null() || detour.is_null() {
            return Err("null method or detour".into());
        }
        // The methodPointer slot: verified live by METHOD_PTR_OFFSET at
        // engine boot (0x00 on this build; name@0x18 klass@0x20 per
        // findings confirm the custom layout).
        let slot = unsafe { method.add(crate::METHOD_PTR_OFFSET.load(std::sync::atomic::Ordering::Acquire)) }
            as *mut NativeFn;
        unsafe {
            let original = *slot;
            if original.is_null() {
                return Err("methodPointer slot is null".into());
            }
            *slot = detour;
            Ok(original)
        }
    }

    fn unhook_method(&mut self, method: MethodPtr, original: NativeFn) -> Result<(), String> {
        let slot = unsafe { method.add(crate::METHOD_PTR_OFFSET.load(std::sync::atomic::Ordering::Acquire)) }
            as *mut NativeFn;
        unsafe { *slot = original };
        Ok(())
    }

    fn hook_method_body(&mut self, method: MethodPtr, detour: NativeFn) -> Result<NativeFn, String> {
        if method.is_null() || detour.is_null() {
            return Err("null method or detour".into());
        }
        let body = unsafe {
            core::ptr::read_unaligned(
                method.add(crate::METHOD_PTR_OFFSET.load(std::sync::atomic::Ordering::Acquire))
                    as *const NativeFn,
            )
        };
        if body.is_null() {
            return Err("method body null".into());
        }
        unsafe {
            aniimod_hook::hook_body(body, detour).map_err(|e| match e {
                aniimod_hook::HookError::NotRelocatable(hex) => {
                    format!("body hook refused, prologue {hex}")
                }
                other => format!("body hook: {other:?}"),
            })
        }
    }

    fn peek_u64(&self, obj: *mut core::ffi::c_void, qword: usize) -> u64 {
        if obj.is_null() {
            return 0;
        }
        unsafe { core::ptr::read_unaligned((obj as *const u8).add(qword * 8) as *const u64) }
    }

    fn on_scene_capture(
        &mut self,
        capture: std::sync::Arc<dyn Fn(&mut dyn Engine) + Send + Sync>,
    ) -> Result<(), String> {
        // whole-value Send/Sync wrapper (RFC 2229 would otherwise capture
        // the raw-pointer field, which is neither Send nor Sync)
        #[derive(Clone, Copy)]
        struct CaptureCtx {
            il2cpp: *const Il2Cpp,
            images: &'static ImageTable,
        }
        unsafe impl Send for CaptureCtx {}
        unsafe impl Sync for CaptureCtx {}
        let ctx = CaptureCtx {
            il2cpp: self.il2cpp,
            images: self.images,
        };
        let capture: Arc<dyn Fn(&mut dyn Engine) + Send + Sync> = capture;
        crate::events::set_scene_capture(Arc::new(move || {
            let ctx = ctx;
            let CaptureCtx { il2cpp, images } = ctx;
            let mut e = EngineImpl {
                mod_name: "capture".into(),
                il2cpp,
                images,
            };
            capture(&mut e);
        }));
        Ok(())
    }

    fn poll_instances(
        &mut self,
        class: ClassPtr,
        every_ms: u64,
        on_instance: std::sync::Arc<dyn Fn(&mut dyn Engine, *mut core::ffi::c_void) + Send + Sync>,
    ) -> Result<(), String> {
        // Safety: function pointers + process-lifetime data; the poll thread
        // attaches itself to the domain before any GC work.
        struct PollCtx {
            il2cpp: *const Il2Cpp,
            images: &'static ImageTable,
            class: ClassPtr,
        }
        unsafe impl Send for PollCtx {}
        let ctx = PollCtx {
            il2cpp: self.il2cpp,
            images: self.images,
            class,
        };
        std::thread::Builder::new()
            .name("aniimod-poll".into())
            .stack_size(1024 * 1024)
            .spawn(move || unsafe {
                // capture the WHOLE (Send) ctx first — RFC 2229 would
                // otherwise capture the individual non-Send fields
                let ctx = ctx;
                let PollCtx { il2cpp, images, class } = ctx;
                ((*il2cpp).thread_attach)(((*il2cpp).domain_get)());
                loop {
                    std::thread::sleep(std::time::Duration::from_millis(every_ms));
                    let mut e = EngineImpl {
                        mod_name: "poll".into(),
                        il2cpp,
                        images,
                    };
                    let Ok(arr) = e.find_objects_of_type(class) else { continue };
                    if arr.is_null() { continue; }
                    let n = e.array_length(arr);
                    for i in 0..n {
                        let inst = e.array_item(arr, i);
                        if inst.is_null() { continue; }
                        on_instance(&mut e, inst);
                    }
                }
            })
            .map_err(|e| format!("poll thread spawn: {e}"))?;
        Ok(())
    }

    fn array_length(&self, arr: *mut core::ffi::c_void) -> i32 {
        if arr.is_null() {
            return 0;
        }
        unsafe { core::ptr::read_unaligned((arr as *const u8).add(0x18) as *const i32) }
    }

    fn array_item(&self, arr: *mut core::ffi::c_void, index: i32) -> *mut core::ffi::c_void {
        if arr.is_null() || index < 0 {
            return core::ptr::null_mut();
        }
        unsafe { core::ptr::read_unaligned((arr as *const u8).add(0x20 + index as usize * 8) as *const *mut core::ffi::c_void) }
    }

    fn find_objects_of_type(&mut self, class: ClassPtr) -> Result<*mut core::ffi::c_void, String> {
        if class.is_null() {
            return Err("null class".into());
        }
        // byval type handle at class+0x50 (verified offset), then its
        // System.Type object, then Object::FindObjectsOfType(Type)
        let type_handle = unsafe {
            core::ptr::read_unaligned((class as *const u8).add(0x50) as *const *mut core::ffi::c_void)
        };
        if type_handle.is_null() {
            return Err("class has null byval type (custom-runtime quirk)".into());
        }
        let il2cpp = unsafe { &*self.il2cpp };
        let managed_type = unsafe { (il2cpp.type_get_object)(type_handle) };
        if managed_type.is_null() {
            return Err("type_get_object returned null".into());
        }
        let objects_class = self.find_class("UnityEngine.CoreModule.dll", "UnityEngine", "Object")?;
        let find = self.find_method(objects_class, "FindObjectsOfType", 1)?;
        self.call_method(find, core::ptr::null_mut(), &[managed_type])
    }

    fn read_field_object(&mut self, obj: *mut core::ffi::c_void, field: FieldPtr) -> Result<*mut core::ffi::c_void, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        if obj.is_null() || field.is_null() {
            return Err("null obj or field".into());
        }
        let mut out: *mut core::ffi::c_void = core::ptr::null_mut();
        unsafe { (il2cpp.field_get_value)(obj, field, &mut out as *mut _ as *mut core::ffi::c_void) };
        Ok(out)
    }

    fn read_field_f32(&mut self, obj: *mut core::ffi::c_void, field: FieldPtr) -> Result<f32, String> {
        let il2cpp = unsafe { &*self.il2cpp };
        if obj.is_null() || field.is_null() {
            return Err("null obj or field".into());
        }
        let mut out: f32 = 0.0;
        unsafe { (il2cpp.field_get_value)(obj, field, &mut out as *mut f32 as *mut core::ffi::c_void) };
        Ok(out)
    }

    fn read_string(&self, s: *mut core::ffi::c_void) -> String {
        if s.is_null() {
            return String::new();
        }
        let len = unsafe { core::ptr::read_unaligned((s as *const u8).add(0x10) as *const u32) } as usize;
        let mut chars = Vec::with_capacity(len);
        for i in 0..len {
            let c = unsafe { core::ptr::read_unaligned((s as *const u8).add(0x14 + i * 2) as *const u16) };
            chars.push(c);
        }
        String::from_utf16_lossy(&chars)
    }
}


fn c_string(s: &str) -> Vec<u8> {
    let mut v = s.as_bytes().to_vec();
    v.push(0);
    v
}

pub struct LoadedMod {
    pub ptr: *mut dyn Mod,
    pub file: String,
}

pub fn load_all(
    log: &dyn Fn(&str),
    il2cpp: &Il2Cpp,
    images: &'static ImageTable,
) -> Vec<LoadedMod> {
    let mut dir = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| PathBuf::from("."));
    dir.push("Mods");
    let _ = std::fs::create_dir_all(&dir);

    let entries = match std::fs::read_dir(&dir) {
        Ok(e) => e,
        Err(e) => {
            log(&format!("M3: Mods dir unreadable: {e}"));
            return Vec::new();
        }
    };

    let il2cpp: *const Il2Cpp = il2cpp;
    let mut loaded: Vec<LoadedMod> = Vec::new();

    for entry in entries.flatten() {
        let path = entry.path();
        if path.extension().and_then(|e| e.to_str()) != Some("dll") {
            continue;
        }
        let file = path.file_name().map(|n| n.to_string_lossy().into_owned()).unwrap_or_default();
        let wide: Vec<u16> = path.as_os_str().to_string_lossy().encode_utf16().chain([0]).collect();
        let module = unsafe { LoadLibraryW(wide.as_ptr()) };
        if module == 0 {
            log(&format!("M3: LoadLibrary {file} failed"));
            continue;
        }
        let entry_fn = unsafe { GetProcAddress(module, b"aniimod_entry\0".as_ptr()) };
        if entry_fn == 0 {
            log(&format!("M3: {file} has no aniimod_entry — skipped"));
            continue;
        }
        let get_mod: unsafe extern "C" fn() -> *mut dyn Mod = unsafe { core::mem::transmute(entry_fn) };
        let result = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| unsafe { get_mod() }));
        let mod_ptr = match result {
            Ok(p) if !p.is_null() => p,
            _ => {
                log(&format!("M3: {file} entry returned null"));
                continue;
            }
        };
        let name = unsafe { (*mod_ptr).name().to_string() };
        log(&format!("M3: loading mod {file} ({name})"));
        let mut engine = EngineImpl {
            mod_name: name,
            il2cpp,
            images,
        };
        let on_load_result = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| unsafe {
            (*mod_ptr).on_load(&mut engine)
        }));
        match on_load_result {
            Ok(Ok(())) => {
                loaded.push(LoadedMod { ptr: mod_ptr, file: file.clone() });
                log(&format!("M3: mod {file} online"));
            }
            Ok(Err(e)) => log(&format!("M3: mod {file} on_load failed: {e}")),
            Err(_) => log(&format!("M3: mod {file} PANICKED in on_load")),
        }
    }
    loaded
}

/// Deferred second lifecycle pass — called on the engine thread after the
/// first scene change (game subsystems up).
pub fn game_ready_all(log: &dyn Fn(&str), mods: &[LoadedMod], il2cpp: &Il2Cpp, images: &'static ImageTable) {
    let il2cpp: *const Il2Cpp = il2cpp;
    for m in mods {
        let mut engine = EngineImpl {
            mod_name: unsafe { (*m.ptr).name().to_string() },
            il2cpp,
            images,
        };
        let r = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| unsafe {
            (*m.ptr).on_game_ready(&mut engine)
        }));
        match r {
            Ok(Ok(())) => log(&format!("game_ready: {} ok", m.file)),
            Ok(Err(e)) => log(&format!("game_ready: {} failed: {e}", m.file)),
            Err(_) => log(&format!("game_ready: {} PANICKED", m.file)),
        }
    }
}

// keep c_void referenced (used in signatures above via type aliases)
#[allow(dead_code)]
type _Keep = *mut c_void;
