//! aniimod-ffi — the il2cpp C API, resolved through the scrambled-export map.
//!
//! Aniimo renames every `il2cpp_*` export, so this crate is the ONLY place
//! that talks to GameAssembly by name: `bind()` takes real names, translates
//! via [`aniimod_map`], and returns typed function pointers obtained with
//! `GetProcAddress`. No P/Invoke remapping, no string rewriting — the C#
//! stack's entire mapping hacks collapse into this lookup.
//!
//! Design notes:
//! * calling convention: il2cpp exports are C ABI (`extern "system"` on the
//!   Windows GNU target ≡ `extern "C"` for these).
//! * start minimal: the domain/image/class/method surface the milestones
//!   need (M0 = enumerate 133 images). Grow per-mod, not speculatively.
//! * every resolved pointer is cached once; binding twice is a bug.

#![allow(clippy::missing_safety_doc)]

use aniimod_map::ExportMap;

pub type Il2CppDomain = *mut core::ffi::c_void;
pub type Il2CppAssembly = *mut core::ffi::c_void;
pub type Il2CppImage = *mut core::ffi::c_void;
pub type Il2CppClass = *mut core::ffi::c_void;
pub type MethodInfo = *mut core::ffi::c_void;
pub type Il2CppString = *mut core::ffi::c_void;

/// Everything bound at boot, behind one struct so misuse fails loudly.
pub type FieldInfo = *mut core::ffi::c_void;

pub struct Il2Cpp {
    gameassembly: isize, // HMODULE of the loaded GameAssembly
    map: ExportMap,
    pub thread_attach: unsafe extern "C" fn(domain: Il2CppDomain) -> *mut core::ffi::c_void,
    pub class_get_method_from_name:
        unsafe extern "C" fn(klass: Il2CppClass, name: *const core::ffi::c_char, argc: i32) -> MethodInfo,
    pub class_get_field_from_name:
        unsafe extern "C" fn(klass: Il2CppClass, name: *const core::ffi::c_char) -> FieldInfo,
    pub field_static_get_value: unsafe extern "C" fn(field: FieldInfo, out: *mut core::ffi::c_void),
    pub field_get_value: unsafe extern "C" fn(obj: *mut core::ffi::c_void, field: FieldInfo, out: *mut core::ffi::c_void),
    pub type_get_object: unsafe extern "C" fn(type_handle: *mut core::ffi::c_void) -> *mut core::ffi::c_void,
    pub string_new: unsafe extern "C" fn(s: *const core::ffi::c_char) -> Il2CppString,
    pub get_corlib: unsafe extern "C" fn() -> Il2CppImage,
    pub class_from_name:
        unsafe extern "C" fn(image: Il2CppImage, ns: *const core::ffi::c_char, name: *const core::ffi::c_char) -> Il2CppClass,
    pub class_get_name: unsafe extern "C" fn(klass: Il2CppClass) -> *const core::ffi::c_char,
    pub method_get_name: unsafe extern "C" fn(method: MethodInfo) -> *const core::ffi::c_char,
    pub domain_get: unsafe extern "C" fn() -> Il2CppDomain,
    pub domain_get_assemblies:
        unsafe extern "C" fn(domain: Il2CppDomain, size: *mut usize) -> *const Il2CppAssembly,
    pub assembly_get_image: unsafe extern "C" fn(assembly: Il2CppAssembly) -> Il2CppImage,
    pub image_get_name: unsafe extern "C" fn(image: Il2CppImage) -> *const core::ffi::c_char,
}

impl Il2Cpp {
    /// Resolve the core API set from a loaded GameAssembly module handle.
    ///
    /// # Safety
    /// `gameassembly` must be a live module handle for the game's
    /// GameAssembly.dll and the export map must match the build.
    pub unsafe fn bind(gameassembly: isize) -> Result<Self, String> {
        let map = ExportMap::baked();
        let get = |real: &str| -> Result<isize, String> {
            let scrambled = map
                .resolve(real)
                .ok_or_else(|| format!("map missing {real}"))?;
            // GetProcAddress via raw FFI; windows targets only in the host
            // build (see aniimod-host). Kept here so the crate has no
            // windows-only dependency for unit tests.
            #[cfg(target_family = "windows")]
            {
                extern "system" {
                    fn GetProcAddress(module: isize, name: *const u8) -> isize;
                }
                let mut buf = scrambled.as_bytes().to_vec();
                buf.push(0);
                let ptr = GetProcAddress(gameassembly, buf.as_ptr());
                if ptr == 0 {
                    Err(format!("export {scrambled} ({real}) not found"))
                } else {
                    Ok(ptr)
                }
            }
            #[cfg(not(target_family = "windows"))]
            {
                let _ = scrambled;
                Err("aniimod-ffi bind() only works inside the game process".into())
            }
        };

        let domain_get: unsafe extern "C" fn() -> Il2CppDomain =
            unsafe { core::mem::transmute(get("il2cpp_domain_get")?) };
        let domain_get_assemblies:
            unsafe extern "C" fn(Il2CppDomain, *mut usize) -> *const Il2CppAssembly =
            unsafe { core::mem::transmute(get("il2cpp_domain_get_assemblies")?) };
        let assembly_get_image: unsafe extern "C" fn(Il2CppAssembly) -> Il2CppImage =
            unsafe { core::mem::transmute(get("il2cpp_assembly_get_image")?) };
        let image_get_name: unsafe extern "C" fn(Il2CppImage) -> *const core::ffi::c_char =
            unsafe { core::mem::transmute(get("il2cpp_image_get_name")?) };
        let thread_attach: unsafe extern "C" fn(Il2CppDomain) -> *mut core::ffi::c_void =
            unsafe { core::mem::transmute(get("il2cpp_thread_attach")?) };
        let class_get_method_from_name: unsafe extern "C" fn(Il2CppClass, *const core::ffi::c_char, i32) -> MethodInfo =
            unsafe { core::mem::transmute(get("il2cpp_class_get_method_from_name")?) };
        let class_get_field_from_name: unsafe extern "C" fn(Il2CppClass, *const core::ffi::c_char) -> FieldInfo =
            unsafe { core::mem::transmute(get("il2cpp_class_get_field_from_name")?) };
        let field_static_get_value: unsafe extern "C" fn(FieldInfo, *mut core::ffi::c_void) =
            unsafe { core::mem::transmute(get("il2cpp_field_static_get_value")?) };
        let field_get_value: unsafe extern "C" fn(*mut core::ffi::c_void, FieldInfo, *mut core::ffi::c_void) =
            unsafe { core::mem::transmute(get("il2cpp_field_get_value")?) };
        let type_get_object: unsafe extern "C" fn(*mut core::ffi::c_void) -> *mut core::ffi::c_void =
            unsafe { core::mem::transmute(get("il2cpp_type_get_object")?) };
        let string_new: unsafe extern "C" fn(*const core::ffi::c_char) -> Il2CppString =
            unsafe { core::mem::transmute(get("il2cpp_string_new")?) };
        let get_corlib: unsafe extern "C" fn() -> Il2CppImage =
            unsafe { core::mem::transmute(get("il2cpp_get_corlib")?) };
        let class_from_name: unsafe extern "C" fn(Il2CppImage, *const core::ffi::c_char, *const core::ffi::c_char) -> Il2CppClass =
            unsafe { core::mem::transmute(get("il2cpp_class_from_name")?) };
        let class_get_name: unsafe extern "C" fn(Il2CppClass) -> *const core::ffi::c_char =
            unsafe { core::mem::transmute(get("il2cpp_class_get_name")?) };
        let method_get_name: unsafe extern "C" fn(MethodInfo) -> *const core::ffi::c_char =
            unsafe { core::mem::transmute(get("il2cpp_method_get_name")?) };
        Ok(Self { gameassembly, map, thread_attach, class_get_method_from_name, class_get_field_from_name, field_static_get_value, field_get_value, type_get_object, string_new, get_corlib, class_from_name, class_get_name, method_get_name, domain_get, domain_get_assemblies, assembly_get_image, image_get_name })
    }

    /// Extract the assemblies-array static addresses by decoding
    /// il2cpp_domain_get_assemblies' own code (`mov rax,[rip+end];
    /// sub rax,[rip+begin]; sar 3; mov [rdx],rax; mov rax,[rip+begin]; ret`).
    ///
    /// CRITICAL LESSON (2026-09-27 crash): il2cpp_domain_get's target is a
    /// CREATE-IF-MISSING singleton — calling it before the game's own
    /// il2cpp_init constructs the domain on the caller's thread and crashes
    /// the game. Readiness must be probed by reading these statics directly,
    /// never by calling into il2cpp.
    pub fn assemblies_vector_statics(&self) -> Result<(usize, usize), String> {
        let f = self.domain_get_assemblies as usize;
        let code = unsafe { core::slice::from_raw_parts(f as *const u8, 32) };
        // mov rax,[rip+d32] = 48 8B 05 xx xx xx xx  (end static, first)
        // sub rax,[rip+d32] = 48 2B 05 xx xx xx xx  (begin static)
        let mut end_static = None;
        let mut begin_static = None;
        let mut i = 0usize;
        while i + 7 <= code.len() {
            if code[i] == 0x48 && code[i + 1] == 0x8B && code[i + 2] == 0x05 && end_static.is_none() {
                let d = i32::from_le_bytes([code[i + 3], code[i + 4], code[i + 5], code[i + 6]]);
                end_static = Some((f + i + 7).wrapping_add(d as usize));
            } else if code[i] == 0x48 && code[i + 1] == 0x2B && code[i + 2] == 0x05 && begin_static.is_none() {
                let d = i32::from_le_bytes([code[i + 3], code[i + 4], code[i + 5], code[i + 6]]);
                begin_static = Some((f + i + 7).wrapping_add(d as usize));
            }
            i += 1;
        }
        match (begin_static, end_static) {
            (Some(b), Some(e)) => Ok((b, e)),
            _ => Err("could not decode assemblies vector statics".into()),
        }
    }

    /// Poll-free readiness check: true once the game's own il2cpp_init has
    /// populated the domain (assemblies array non-empty). Read-only.
    ///
    /// # Safety
    /// `begin`/`end` must come from [`Self::assemblies_vector_statics`].
    pub unsafe fn domain_ready(begin: usize, end: usize) -> bool {
        if begin == 0 || end == 0 {
            return false;
        }
        let b = core::ptr::read_volatile(begin as *const usize);
        let e = core::ptr::read_volatile(end as *const usize);
        b != 0 && e >= b && (e - b) % core::mem::size_of::<usize>() == 0 && e > b
    }

    /// Enumerate loaded image names (milestone M0's proof: 133 entries).
    /// # Safety
    /// Must be called after `il2cpp_init` completed on this thread.
    pub unsafe fn image_names(&self) -> Vec<String> {
        let domain = (self.domain_get)();
        let mut count = 0usize;
        let assemblies = (self.domain_get_assemblies)(domain, &mut count);
        let mut names = Vec::with_capacity(count);
        for i in 0..count {
            let image = (self.assembly_get_image)(*assemblies.add(i));
            let name = (self.image_get_name)(image);
            names.push(core::ffi::CStr::from_ptr(name).to_string_lossy().into_owned());
        }
        names
    }
}
