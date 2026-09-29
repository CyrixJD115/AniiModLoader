//! aniimod-mod — the mod SDK.
//!
//! Mods are `cdylib`s in `<game>/Mods/` exporting
//! `aniimod_entry() -> *mut dyn Mod` (hand-write the export — see
//! mods/anii-example). The host loads them AFTER the il2cpp domain is up
//! and calls `Mod::on_load` with an [`Engine`] implementation.
//!
//! Build rule: mods MUST be compiled with the same rustc as the host
//! (trait-object vtables move between compiler versions).

pub type ClassPtr = *mut core::ffi::c_void;
pub type MethodPtr = *mut core::ffi::c_void;
pub type FieldPtr = *mut core::ffi::c_void;
pub type NativeFn = *mut core::ffi::c_void;
pub type SceneCallback = Box<dyn Fn(u64) + Send>;

/// What the host exposes to mods. Every capability is read-only,
/// event-driven, or routed through the host's own bridges — mods never
/// touch GameAssembly directly.
pub trait Engine {
    /// Structured log into AniiModLoader/AniiModRust.log + the console
    /// (mod name is prefixed by the host).
    fn log(&mut self, message: &str);

    /// Resolve an il2cpp class by image + namespace + name.
    fn find_class(&mut self, image: &str, namespace: &str, name: &str) -> Result<ClassPtr, String>;

    /// Subscribe to scene-change events (counter argument). Called on a
    /// game thread — keep it fast and non-blocking.
    fn on_scene_changed(&mut self, callback: SceneCallback) -> Result<(), String>;

    // ---- M5: calling into the game ----

    /// Resolve a method on a class (argc disambiguates overloads).
    fn find_method(&mut self, class: ClassPtr, name: &str, argc: i32) -> Result<MethodPtr, String>;

    /// Resolve a field on a class by name.
    fn find_field(&mut self, class: ClassPtr, name: &str) -> Result<FieldPtr, String>;

    /// Create an il2cpp string (GC-allocated; engine thread is attached).
    fn make_string(&mut self, s: &str) -> Result<*mut core::ffi::c_void, String>;

    /// Invoke a (static or instance) method through the host's captured
    /// runtime_invoke. `args` are argument VALUES (object references for
    /// reference types; pass the address/boxed value for scalars).
    /// Returns runtime_invoke's result (boxed value for value-type returns,
    /// null for void).
    fn call_method(
        &mut self,
        method: MethodPtr,
        obj: *mut core::ffi::c_void,
        args: &[*mut core::ffi::c_void],
    ) -> Result<*mut core::ffi::c_void, String>;

    /// Read the f32 payload of a boxed result (value at object+0x10).
    fn unbox_f32(&self, boxed: *mut core::ffi::c_void) -> f32;

    /// Character length of an il2cpp string (dword at +0x10).
    fn string_length(&self, s: *mut core::ffi::c_void) -> u32;

    /// Read a static object-reference field.
    fn read_static_field(&mut self, field: FieldPtr) -> Result<*mut core::ffi::c_void, String>;

    // ---- M6: method hooking ----

    /// Swap the method's native entry (the methodPointer slot inside the
    /// heap MethodInfo — a DATA write, no code pages touched) for `detour`.
    /// Only intercepts runtime_invoke/reflection paths.
    fn hook_method(&mut self, method: MethodPtr, detour: NativeFn) -> Result<NativeFn, String>;

    /// Patch the method's BODY so ALL callers (direct game calls included)
    /// land in `detour`. Returns the trampoline that runs the original.
    /// Refuses (error) when the prologue isn't safely relocatable.
    fn hook_method_body(&mut self, method: MethodPtr, detour: NativeFn) -> Result<NativeFn, String>;

    /// Read the n-th qword of a GC object (diagnostics/advanced use).
    fn peek_u64(&self, obj: *mut core::ffi::c_void, qword: usize) -> u64;

    // ---- poll-capture surface (tamper-safe: reads + invoke only) ----

    /// Length of an il2cpp array object (int at +0x18).
    fn array_length(&self, arr: *mut core::ffi::c_void) -> i32;

    /// n-th element reference of an il2cpp array (data at +0x20).
    fn array_item(&self, arr: *mut core::ffi::c_void, index: i32) -> *mut core::ffi::c_void;

    /// UnityEngine.Object::FindObjectsOfType(T) for the mod's resolved
    /// class — returns the il2cpp array of live instances.
    fn find_objects_of_type(&mut self, class: ClassPtr) -> Result<*mut core::ffi::c_void, String>;

    /// Read an object-reference instance field.
    fn read_field_object(&mut self, obj: *mut core::ffi::c_void, field: FieldPtr) -> Result<*mut core::ffi::c_void, String>;

    /// Read an f32 instance field.
    fn read_field_f32(&mut self, obj: *mut core::ffi::c_void, field: FieldPtr) -> Result<f32, String>;

    /// Read an il2cpp string's chars as UTF-8 (best effort).
    fn read_string(&self, s: *mut core::ffi::c_void) -> String;

    /// Poll-capture (tamper-safe): the host spawns a dedicated attached
    /// thread that every `every_ms` calls FindObjectsOfType(class) and
    /// invokes `on_instance` for each live instance. The callback runs on
    /// the poll thread with a usable Engine — read fields there.
    fn poll_instances(
        &mut self,
        class: ClassPtr,
        every_ms: u64,
        on_instance: std::sync::Arc<dyn Fn(&mut dyn Engine, *mut core::ffi::c_void) + Send + Sync>,
    ) -> Result<(), String>;

    /// TAMPER-SAFE CAPTURE (preferred): run `capture` on the GAME'S OWN
    /// THREAD after each scene change (3rd onward). Foreign-thread invokes
    /// of engine-allocating APIs (FindObjectsOfType etc.) crash this
    /// custom runtime — game-thread work does not.
    fn on_scene_capture(
        &mut self,
        capture: std::sync::Arc<dyn Fn(&mut dyn Engine) + Send + Sync>,
    ) -> Result<(), String>;

    /// Restore a hooked method's original entry.
    fn unhook_method(&mut self, method: MethodPtr, original: NativeFn) -> Result<(), String>;
}

/// A mod. Keep `on_load` fast: it runs on the engine thread while the
/// game boots.
pub trait Mod {
    fn name(&self) -> &str {
        "unnamed"
    }

    fn on_load(&mut self, engine: &mut dyn Engine) -> Result<(), String> {
        let _ = engine;
        Ok(())
    }

    /// Called ONCE on the engine thread after the first scene change —
    /// Unity subsystems exist by then, so method invocation is safe.
    /// Anything that touches engine state belongs here, not in on_load
    /// (the 2026-09-27 lesson: Time.get_time at +0.2 s crashed the game).
    fn on_game_ready(&mut self, engine: &mut dyn Engine) -> Result<(), String> {
        let _ = engine;
        Ok(())
    }

    fn on_unload(&mut self) {}
}
