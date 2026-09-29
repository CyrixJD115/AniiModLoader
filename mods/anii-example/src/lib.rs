//! anii-example — SDK walkthrough mod.
//! M3: log/class-resolution/scene events. M5: call game methods, read
//! static fields, log through the game's own Debug.Log.

#![cfg(target_family = "windows")]

use aniimod_mod::{Engine, Mod};

pub struct Example;

impl Mod for Example {
    fn name(&self) -> &str {
        "anii-example"
    }

    fn on_load(&mut self, engine: &mut dyn Engine) -> Result<(), String> {
        engine.log("online (Rust engine)");

        // class resolution
        let string_class = engine.find_class("mscorlib.dll", "System", "String")?;
        let game_object = engine
            .find_class("UnityEngine.CoreModule.dll", "UnityEngine", "GameObject")
            .map(|p| format!("{p:p}"))
            .unwrap_or_else(|e| format!("miss ({e})"));
        engine.log(&format!("GameObject: {game_object}"));

        // ---- M5a: read a static field (System.String::Empty) ----
        match engine.find_field(string_class, "Empty").and_then(|f| engine.read_static_field(f)) {
            Ok(empty) => engine.log(&format!(
                "M5 String::Empty len = {} (expect 0)",
                engine.string_length(empty)
            )),
            Err(e) => engine.log(&format!("M5 static field miss: {e}")),
        }

        engine.on_scene_changed(Box::new(|n| {
            let _ = n;
        }))?;
        engine.log("on_load done (invoke tests deferred to on_game_ready)");
        Ok(())
    }

    fn on_game_ready(&mut self, engine: &mut dyn Engine) -> Result<(), String> {
        // ---- M5b: call a static getter (subsystems are up now) ----
        let time_class = engine.find_class("UnityEngine.CoreModule.dll", "UnityEngine", "Time")?;
        match engine.find_method(time_class, "get_time", 0) {
            Ok(get_time) => match engine.call_method(get_time, core::ptr::null_mut(), &[]) {
                Ok(boxed) => {
                    let single = engine.find_class("mscorlib.dll", "System", "Single")?;
                    engine.log(&format!(
                        "M5 get_time box: klass={:#x} (Single={single:p}) q1={:#x} q2={:#x} q3={:#x} q4={:#x}",
                        engine.peek_u64(boxed, 0),
                        engine.peek_u64(boxed, 1),
                        engine.peek_u64(boxed, 2),
                        engine.peek_u64(boxed, 3),
                        engine.peek_u64(boxed, 4),
                    ));
                }
                Err(e) => engine.log(&format!("M5 get_time invoke miss: {e}")),
            },
            Err(e) => engine.log(&format!("M5 get_time miss: {e}")),
        }

        // ---- M5c: second static getter cross-check ----
        // (Debug.Log-from-foreign-thread crash 2026-09-27: its body lazily
        // inits a class with null byval on this custom runtime — documented,
        // avoided. Simple property getters are safe.)
        match engine.find_method(time_class, "get_realtimeSinceStartup", 0) {
            Ok(m) => match engine.call_method(m, core::ptr::null_mut(), &[]) {
                Ok(boxed) => {
                    let t = engine.unbox_f32(boxed);
                    engine.log(&format!(
                        "M5 get_realtimeSinceStartup() = {t:.1}s {}",
                        if t > 1.0 { "(plausible — invoke+unbox PROVEN)" } else { "(SUSPICIOUS)" }
                    ));
                }
                Err(e) => engine.log(&format!("M5 realtime miss: {e}")),
            },
            Err(e) => engine.log(&format!("M5 realtime method miss: {e}")),
        }

        engine.log("M5 COMPLETE");
        Ok(())
    }
}

#[no_mangle]
pub extern "C" fn aniimod_entry() -> *mut dyn Mod {
    Box::into_raw(Box::new(Example))
}
