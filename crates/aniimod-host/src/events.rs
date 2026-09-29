//! Event taps (M2): the GetProcAddress detour hands the engine our
//! `il2cpp_init` and `il2cpp_runtime_invoke` when it asks for their
//! scrambled names; the invoke detour watches for scene changes by reading
//! the method name straight out of the MethodInfo (offset 0x18) — no
//! allocation on the hot path.

#![allow(clippy::missing_safety_doc)]

use std::sync::atomic::{AtomicU64, AtomicUsize, Ordering};
use std::sync::{Arc, Mutex, OnceLock};

use aniimod_hook::bytes_eq_z;
use aniimod_map::ExportMap;

// Scrambled export names, resolved from the verified map at first use —
// a game update that rescrambles names needs only a new map asset, never
// code edits here. runtime_invoke's identity is additionally body-proven
// (see aniimod-map docs).
fn scrambled_z(real: &str) -> Vec<u8> {
    let mut v = ExportMap::baked()
        .resolve(real)
        .map(|s| s.as_bytes().to_vec())
        .unwrap_or_default();
    v.push(0);
    v
}

static S_INIT: OnceLock<Vec<u8>> = OnceLock::new();
static S_RUNTIME_INVOKE: OnceLock<Vec<u8>> = OnceLock::new();

pub fn s_init() -> &'static [u8] {
    S_INIT.get_or_init(|| scrambled_z("il2cpp_init"))
}

pub fn s_runtime_invoke() -> &'static [u8] {
    S_RUNTIME_INVOKE.get_or_init(|| scrambled_z("il2cpp_runtime_invoke"))
}
// the event marker we watch for inside the invoke stream
const SCENE_CHANGED: &[u8] = b"Internal_ActiveSceneChanged";

pub type SceneCallback = Box<dyn Fn(u64) + Send>;

static REAL_GPA: AtomicUsize = AtomicUsize::new(0);
static REAL_INIT: AtomicUsize = AtomicUsize::new(0);
static REAL_INVOKE: AtomicUsize = AtomicUsize::new(0);
static REAL_CREATE_FILE_W: AtomicUsize = AtomicUsize::new(0);
static REAL_CREATE_FILE_A: AtomicUsize = AtomicUsize::new(0);
static REAL_GETADDRINFO: AtomicUsize = AtomicUsize::new(0);
static REAL_GETADDRINFO_W: AtomicUsize = AtomicUsize::new(0);

pub fn real_create_file_w() -> Option<
    unsafe extern "system" fn(
        *const u16,
        u32,
        u32,
        *mut core::ffi::c_void,
        u32,
        u32,
        isize,
    ) -> isize,
> {
    let p = REAL_CREATE_FILE_W.load(Ordering::Acquire);
    if p == 0 { None } else { Some(unsafe { core::mem::transmute(p) }) }
}

pub fn real_create_file_a() -> Option<
    unsafe extern "system" fn(
        *const u8,
        u32,
        u32,
        *mut core::ffi::c_void,
        u32,
        u32,
        isize,
    ) -> isize,
> {
    let p = REAL_CREATE_FILE_A.load(Ordering::Acquire);
    if p == 0 { None } else { Some(unsafe { core::mem::transmute(p) }) }
}

pub fn real_getaddrinfo() -> Option<
    unsafe extern "system" fn(*const u8, *const u8, *const core::ffi::c_void, *mut *mut core::ffi::c_void) -> i32,
> {
    let p = REAL_GETADDRINFO.load(Ordering::Acquire);
    if p == 0 { None } else { Some(unsafe { core::mem::transmute(p) }) }
}

pub fn real_getaddrinfo_w() -> Option<
    unsafe extern "system" fn(*const u16, *const u16, *const core::ffi::c_void, *mut *mut core::ffi::c_void) -> i32,
> {
    let p = REAL_GETADDRINFO_W.load(Ordering::Acquire);
    if p == 0 { None } else { Some(unsafe { core::mem::transmute(p) }) }
}

static SCENE_COUNT: AtomicU64 = AtomicU64::new(0);
static INIT_SEEN: AtomicUsize = AtomicUsize::new(0);
static INVOKE_SEEN: AtomicUsize = AtomicUsize::new(0);

static SCENE_CALLBACKS: Mutex<Option<Vec<SceneCallback>>> = Mutex::new(None);
/// M7: capture callbacks run ON THE GAME THREAD at scene changes (after
/// the original invoke) — foreign-thread invokes of engine-allocating APIs
/// crash this custom runtime, so all capture work happens here.
static SCENE_CAPTURE: Mutex<Option<Arc<dyn Fn() + Send + Sync>>> = Mutex::new(None);
static SCENES_SEEN: AtomicU64 = AtomicU64::new(0);

/// install the GetProcAddress IAT swap (call once from DllMain; cheap,
/// loader-lock safe). Returns Err when the import entry is missing.
pub unsafe fn install(log: &dyn Fn(&str)) -> Result<(), String> {
    #[link(name = "kernel32")]
    extern "system" {
        fn GetModuleHandleW(name: *const u16) -> isize;
    }
    let wide: Vec<u16> = "UnityPlayer.dll\0".encode_utf16().collect();
    let module = GetModuleHandleW(wide.as_ptr());
    if module == 0 {
        return Err("UnityPlayer.dll not mapped yet".into());
    }
    let detour = gpa_detour as *const core::ffi::c_void;
    let original = aniimod_hook::swap_module_import(module, "kernel32.dll", "GetProcAddress", detour)
        .map_err(|e| format!("IAT swap failed: {e:?}"))?;
    REAL_GPA.store(original as usize, Ordering::Release);
    log("M2: GetProcAddress IAT swap installed on UnityPlayer.dll");
    Ok(())
}

/// Deterministic M7/M8 arming: swap the RESOLVED CreateFileW / getaddrinfo
/// / GetAddrInfoW addresses in the IATs of every module that imports them
/// (ordinal imports included — the name-based walk misses them, which is
/// why the GetProcAddress-tap arming was boot-dependent). Data-page writes
/// only; call once GameAssembly and the CRT/plugin DLLs are mapped.
pub unsafe fn install_address_swaps(log: &dyn Fn(&str)) {
    #[link(name = "kernel32")]
    extern "system" {
        fn GetModuleHandleW(name: *const u16) -> isize;
    }
    fn handle_of(name: &str) -> isize {
        #[link(name = "kernel32")]
        extern "system" {
            fn GetModuleHandleW(name: *const u16) -> isize;
        }
        let wide: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
        unsafe { GetModuleHandleW(wide.as_ptr()) }
    }

    let (n_cfw, n_dns) = resweep(false);
    log(&format!(
        "sweep: CreateFileW/A {n_cfw} slot(s), resolver {n_dns} slot(s) swapped"
    ));
}

/// One idempotent sweep pass over the target modules. Slots already
/// pointing at a detour (or whose module is not yet mapped) are skipped,
/// so late-loaded DLLs — FPX.dll maps AFTER the engine sweep and reads
/// fpx_config_pc through CreateFileA by ordinal (live 2026-09-28: the
/// fpx redirect never armed) — get patched by the poller's later passes.
/// Returns (file-slot swaps, resolver-slot swaps) performed THIS pass.
pub unsafe fn resweep(_verbose: bool) -> (usize, usize) {
    #[link(name = "kernel32")]
    extern "system" {
        fn GetModuleHandleW(name: *const u16) -> isize;
    }
    fn handle_of(name: &str) -> isize {
        #[link(name = "kernel32")]
        extern "system" {
            fn GetModuleHandleW(name: *const u16) -> isize;
        }
        let wide: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
        unsafe { GetModuleHandleW(wide.as_ptr()) }
    }

    // Resolve the real addresses through OUR import of kernel32/ws2_32 —
    // version.dll's own IAT is never tapped, so this is a clean call.
    let k32 = handle_of("kernel32.dll");
    let ws2 = handle_of("ws2_32.dll");
    if k32 == 0 {
        return (0, 0);
    }
    let real_cfw = crate::sys::get_proc_address(k32, b"CreateFileW\0".as_ptr());
    let real_cfa = crate::sys::get_proc_address(k32, b"CreateFileA\0".as_ptr());
    let real_ga = if ws2 != 0 { crate::sys::get_proc_address(ws2, b"getaddrinfo\0".as_ptr()) } else { 0 };
    let real_gaw = if ws2 != 0 { crate::sys::get_proc_address(ws2, b"GetAddrInfoW\0".as_ptr()) } else { 0 };
    if real_cfw == 0 {
        return (0, 0);
    }
    REAL_CREATE_FILE_W.store(real_cfw as usize, Ordering::Release);
    if real_cfa != 0 {
        REAL_CREATE_FILE_A.store(real_cfa as usize, Ordering::Release);
    }
    if real_ga != 0 {
        REAL_GETADDRINFO.store(real_ga as usize, Ordering::Release);
    }
    if real_gaw != 0 {
        REAL_GETADDRINFO_W.store(real_gaw as usize, Ordering::Release);
    }

    // The proven hardcoded target list — module ENUMERATION is banned
    // here (two live hangs 2026-09-28: wine's K32EnumProcessModules
    // stalled the boot both in probe form AND bounded form). Late loaders
    // are caught by the resweep poller via this list instead.
    let targets = [
        "UnityPlayer.dll",
        "GameAssembly.dll",
        "msvcp140.dll",
        "vcruntime140.dll",
        "vcruntime140_1.dll",
        "ucrtbase.dll",
        "xlua.dll",
        "FPX.dll",
        "FPXGame.dll",
        "FPXSteam.dll",
        "PixVideo.dll",
        "GMESDK.dll",
        "sqlite3worldx.dll",
    ];
    let cfw_detour = crate::net::create_file_w_detour as *const core::ffi::c_void;
    let cfa_detour = crate::net::create_file_a_detour as *const core::ffi::c_void;
    let ga_detour = crate::net::getaddrinfo_detour as *const core::ffi::c_void;
    let gaw_detour = crate::net::getaddrinfo_w_detour as *const core::ffi::c_void;
    let mut n_cfw = 0usize;
    let mut n_dns = 0usize;
    for t in targets {
        let m = handle_of(t);
        if m == 0 {
            continue;
        }
        n_cfw += aniimod_hook::swap_module_import_by_address(m, "kernel32.dll", real_cfw as usize, cfw_detour);
        if real_cfa != 0 {
            n_cfw += aniimod_hook::swap_module_import_by_address(m, "kernel32.dll", real_cfa as usize, cfa_detour);
        }
        if real_ga != 0 {
            n_dns += aniimod_hook::swap_module_import_by_address(m, "ws2_32.dll", real_ga as usize, ga_detour);
        }
        if real_gaw != 0 {
            n_dns += aniimod_hook::swap_module_import_by_address(m, "ws2_32.dll", real_gaw as usize, gaw_detour);
        }
    }
    (n_cfw, n_dns)
}


/// Register a capture routine executed on the game thread after each
/// scene-change invoke completes.
pub fn set_scene_capture(cb: Arc<dyn Fn() + Send + Sync>) {
    let mut guard = SCENE_CAPTURE.lock().unwrap_or_else(|e| e.into_inner());
    *guard = Some(cb);
}

pub fn subscribe_scene(cb: SceneCallback) {
    let mut guard = SCENE_CALLBACKS.lock().unwrap_or_else(|e| e.into_inner());
    guard.get_or_insert_with(Vec::new).push(cb);
}

pub fn init_seen() -> bool {
    INIT_SEEN.load(Ordering::Acquire) != 0
}

pub fn scene_count() -> u64 {
    SCENE_COUNT.load(Ordering::Acquire)
}

pub fn invoke_seen() -> bool {
    INVOKE_SEEN.load(Ordering::Acquire) != 0
}

/// The detour the engine's GetProcAddress calls land in. MUST stay
/// allocation-free: it runs on every symbol lookup.
unsafe extern "system" fn gpa_detour(module: isize, name: *const u8) -> isize {
    let real = REAL_GPA.load(Ordering::Acquire);
    if real == 0 {
        return 0;
    }
    let real: unsafe extern "system" fn(isize, *const u8) -> isize = core::mem::transmute(real);
    let original = real(module, name);
    if name.is_null() {
        return original;
    }
    if bytes_eq_z(name, s_init()) {
        REAL_INIT.store(original as usize, Ordering::Release);
        crate::log_line("M2: engine requested il2cpp_init — detour armed");
        return init_detour as isize;
    }
    if bytes_eq_z(name, s_runtime_invoke()) {
        REAL_INVOKE.store(original as usize, Ordering::Release);
        crate::log_line("M2: engine requested il2cpp_runtime_invoke — tap armed");
        return invoke_detour as isize;
    }
    if bytes_eq_z(name, b"CreateFileW") {
        REAL_CREATE_FILE_W.store(original as usize, Ordering::Release);
        return crate::net::create_file_w_detour as isize;
    }
    if bytes_eq_z(name, b"getaddrinfo") {
        REAL_GETADDRINFO.store(original as usize, Ordering::Release);
        return crate::net::getaddrinfo_detour as isize;
    }
    if bytes_eq_z(name, b"GetAddrInfoW") {
        REAL_GETADDRINFO_W.store(original as usize, Ordering::Release);
        return crate::net::getaddrinfo_w_detour as isize;
    }
    original
}

unsafe extern "C" fn init_detour(a: *mut core::ffi::c_void) -> *mut core::ffi::c_void {
    let real = REAL_INIT.load(Ordering::Acquire);
    if real != 0 {
        if INIT_SEEN.swap(1, Ordering::AcqRel) == 0 {
            crate::log_line("M2: il2cpp_init intercepted (original called)");
        }
        let real: unsafe extern "C" fn(*mut core::ffi::c_void) -> *mut core::ffi::c_void =
            core::mem::transmute(real);
        return real(a);
    }
    core::ptr::null_mut()
}

/// Runs on EVERY managed invocation — keep it tight: call the original,
/// then one pointer read + byte-compare against the scene marker.
unsafe extern "C" fn invoke_detour(
    method: *mut core::ffi::c_void,
    obj: *mut core::ffi::c_void,
    params: *mut core::ffi::c_void,
    exc: *mut core::ffi::c_void,
) -> *mut core::ffi::c_void {
    let real = REAL_INVOKE.load(Ordering::Acquire);
    let result = if real != 0 {
        let real: unsafe extern "C" fn(
            *mut core::ffi::c_void,
            *mut core::ffi::c_void,
            *mut core::ffi::c_void,
            *mut core::ffi::c_void,
        ) -> *mut core::ffi::c_void = core::mem::transmute(real);
        real(method, obj, params, exc)
    } else {
        core::ptr::null_mut()
    };

    if !method.is_null() {
        INVOKE_SEEN.store(1, Ordering::Release);
        // MethodInfo+0x18 = name (findings; the [rcx+0x18] reader twin)
        let name = *(method as *const *const u8).add(3);
        if !name.is_null() && bytes_eq_z(name, SCENE_CHANGED) {
            let n = SCENE_COUNT.fetch_add(1, Ordering::AcqRel) + 1;
            crate::log_line(&format!("scene event #{n} (Internal_ActiveSceneChanged)"));
            if let Ok(mut guard) = SCENE_CALLBACKS.lock() {
                if let Some(cbs) = guard.as_mut() {
                    for cb in cbs.iter() {
                        cb(n);
                    }
                }
            }
        }
    }
    result
}

/// The ORIGINAL il2cpp_runtime_invoke our tap captured (the engine resolved
/// it through us at boot). Mods call game methods through this — no new
/// hooks required.
pub fn original_invoke() -> Option<unsafe extern "C" fn(*mut c_void, *mut c_void, *mut c_void, *mut c_void) -> *mut c_void> {
    let p = REAL_INVOKE.load(Ordering::Acquire);
    if p == 0 {
        None
    } else {
        Some(unsafe { core::mem::transmute(p) })
    }
}

use core::ffi::c_void;
