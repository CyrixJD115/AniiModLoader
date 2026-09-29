//! anii-signal (Rust) — telemetry/server-list mod on the Rust engine.
//! Capture = POLLING (tamper-safe): no GameAssembly code modification, so
//! the anti-cheat sweep never sees us. See docs/findings.md for why body
//! patches are banned on this game.

#![cfg(target_family = "windows")]

use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Instant;

use aniimod_mod::{ClassPtr, Engine, Mod};

#[derive(Default)]
struct SignalConfig {
    office_base: String,      // e.g. http://127.0.0.1:18081 (Serviimo office)
    telemetry_path: String,   // /api/aniisignal/telemetry
    flush_seconds: u64,
}

fn load_config() -> SignalConfig {
    let mut cfg = SignalConfig {
        office_base: "http://127.0.0.1:18081".into(),
        telemetry_path: "/api/aniisignal/telemetry".into(),
        flush_seconds: 2,
    };
    let mut path = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_default();
    path.push("Mods");
    path.push("AniiSignal.json");
    if let Ok(text) = std::fs::read_to_string(&path) {
        if let Ok(v) = serde_json::from_str::<serde_json::Value>(&text) {
            if let Some(s) = v["OfficeBaseUrl"].as_str() {
                cfg.office_base = s.trim_end_matches('/').to_string();
            }
            if let Some(s) = v["TelemetryPath"].as_str() {
                cfg.telemetry_path = s.to_string();
            }
            if let Some(n) = v["TelemetryFlushSeconds"].as_u64() {
                cfg.flush_seconds = n.max(1);
            }
        }
    }
    cfg
}

/// Raw class pointer with explicit Send (function-handle-like: points into
/// il2cpp's type system, valid for the process lifetime).
#[derive(Clone, Copy)]
struct SendClass(ClassPtr);
unsafe impl Send for SendClass {}
unsafe impl Sync for SendClass {}

/// Field handles with explicit Send (stable il2cpp type-system pointers).
#[derive(Clone, Copy)]
struct FieldHandles {
    owner: *mut core::ffi::c_void,
    pos: Option<*mut core::ffi::c_void>,
}
unsafe impl Send for FieldHandles {}
unsafe impl Sync for FieldHandles {}

/// FieldPtr with explicit Send (stable il2cpp metadata pointer).
#[derive(Clone, Copy)]
struct FieldPtrSend(aniimod_mod::FieldPtr);
unsafe impl Send for FieldPtrSend {}
unsafe impl Sync for FieldPtrSend {}

static NOW: OnceLock<Instant> = OnceLock::new();
static INSTANCE_TICKS: AtomicU64 = AtomicU64::new(0);

pub struct AniiSignal;

impl Mod for AniiSignal {
    fn name(&self) -> &str {
        "anii-signal"
    }

    fn on_load(&mut self, engine: &mut dyn Engine) -> Result<(), String> {
        let _ = NOW.set(Instant::now());
        engine.log("v0.3.1-rs online — scene capture + entity ids");

        let poll_class: ClassPtr = engine.find_class(
            "FunPlus.WorldX.Scripts.dll",
            "FunPlus.WorldX.Entities.Components",
            "PosSyncComponent",
        )?;
        engine.log("[resolve] PosSyncComponent ok (no pos field by design — transform chain is authoritative)");

        // Entity identity (dump-verified): BaseComponent.<owner>k__BackingField
        // → Entity.<id>k__BackingField (string). Both name-driven, so a
        // metadata change fails loudly here instead of silently in capture.
        let base_class = engine.find_class(
            "FunPlus.WorldX.Scripts.dll",
            "FunPlus.WorldX.Entities.Components",
            "BaseComponent",
        );
        let entity_id_field = match base_class
            .ok()
            .and_then(|c| engine.find_field(c, "<owner>k__BackingField").ok())
        {
            Some(owner_f) => {
                let entity_class = engine.find_class(
                    "FunPlus.WorldX.Scripts.dll",
                    "FunPlus.WorldX.Entities",
                    "Entity",
                );
                match entity_class.ok().and_then(|c| engine.find_field(c, "<id>k__BackingField").ok()) {
                    Some(id_f) => {
                        engine.log("[resolve] entity chain ok: owner → Entity.id (string)");
                        Some((owner_f, id_f))
                    }
                    None => {
                        engine.log("[resolve] Entity.<id> missing — falling back to pointer ids");
                        None
                    }
                }
            }
            None => {
                engine.log("[resolve] BaseComponent.<owner> missing — falling back to pointer ids");
                None
            }
        };

        // Field handles are stable pointers — resolve lazily on first
        // callback (we're on the poll thread there, engine usable).
        let fields: Arc<Mutex<Option<FieldHandles>>> = Arc::new(Mutex::new(None));
        let _fields_for_cb = fields.clone();
        let poll_class = SendClass(poll_class);
        let entity_chain = entity_id_field.map(|(o, i)| (FieldPtrSend(o), FieldPtrSend(i)));

        // ---- M7: config + telemetry channel ----
        let cfg = load_config();
        let telemetry_url = format!("{}{}", cfg.office_base, cfg.telemetry_path);
        let samples: Arc<Mutex<Vec<Sample>>> = Arc::new(Mutex::new(Vec::new()));
        let samples_flush = samples.clone();
        let flush_every = cfg.flush_seconds;
        std::thread::Builder::new()
            .name("anii-signal-flush".into())
            .spawn(move || loop {
                std::thread::sleep(std::time::Duration::from_secs(flush_every));
                let batch: Vec<Sample> = samples_flush
                    .lock()
                    .unwrap_or_else(|e| e.into_inner())
                    .drain(..)
                    .collect();
                if batch.is_empty() {
                    continue;
                }
                if let Err(e) = post_telemetry(&telemetry_url, &batch) {
                    log_throttled("telemetry", &format!("POST failed: {e}"), 10_000);
                    // keep samples? drop to avoid unbounded growth; next batch continues
                } else {
                    log_throttled(
                        "telemetry",
                        &format!("POSTed {} sample(s)", batch.len()),
                        10_000,
                    );
                }
            })
            .map_err(|e| format!("flush thread: {e}"))?;

        // ---- M7 capture: on the GAME'S OWN THREAD at scene changes ----
        // (foreign-thread invokes of engine-allocating APIs crash this
        // runtime; game-thread scene-change work does not)
        let samples_cb = samples.clone();
        let entity_chain_cb = entity_chain;
        engine.on_scene_capture(Arc::new(move |engine: &mut dyn Engine| {
            write_log("scene-capture tick (game thread)");
            let poll_class = match engine.find_class(
                "FunPlus.WorldX.Scripts.dll",
                "FunPlus.WorldX.Entities.Components",
                "PosSyncComponent",
            ) {
                Ok(c) => c,
                Err(e) => {
                    log_throttled("capture", &format!("class miss: {e}"), 30_000);
                    return;
                }
            };
            let arr = match engine.find_objects_of_type(poll_class) {
                Ok(a) => a,
                Err(e) => {
                    log_throttled("capture", &format!("find failed: {e}"), 30_000);
                    return;
                }
            };
            if arr.is_null() { return; }
            let n = engine.array_length(arr);
            let mut captured = 0usize;
            for i in 0..n {
                let inst = engine.array_item(arr, i);
                if inst.is_null() { continue; }
                let (x, y, z) = read_transform_position(engine, inst);
                if x.is_nan() { continue; }
                let id = match entity_chain_cb {
                    Some((owner_f, id_f)) => entity_id_string(engine, inst, owner_f.0, id_f.0)
                        .unwrap_or_else(|| format!("ps-{:x}", inst as usize)),
                    None => format!("ps-{:x}", inst as usize),
                };
                samples_cb
                    .lock()
                    .unwrap_or_else(|e| e.into_inner())
                    .push(Sample {
                        entity: id,
                        x: x as f64,
                        y: y as f64,
                        z: z as f64,
                        t: NOW.get()
                            .map(|t| t.elapsed().as_millis() as i64)
                            .unwrap_or(0),
                    });
                captured += 1;
            }
            log_throttled(
                "capture",
                &format!("scene scan: {n} component(s), {captured} with position"),
                4000,
            );
        }))?;

        engine.log("M7 capture armed (scene-thread, tamper-safe) → Serviimo telemetry");

        // ---- V3-update diagnostic: login-video "Illegal characters in path" ----
        // Player.log 2026-09-28 (new update, wine): Path.GetFileName(type)
        // throws inside RawFileCreateType2Path <- RawFileResIDToRelativePath
        // <- PVideoController.OpenVideo, so no video ever opens. The
        // redirect is NOT armed (no Serviimo XDF staged) — the game is
        // reading its own shipping Lua. Capture the exact strings entering
        // the raw-file path builder; slot swap only, tamper-safe.
        let rm_class = engine.find_class(
            "FunPlus.WorldX.ResourceManager.Runtime.dll",
            "FunPlus.WorldX.ResourceManager.Runtime",
            "WorldXResourceManager",
        );
        if let Ok(rm) = rm_class {
            match engine.find_method(rm, "RawFileResIDToRelativePath", 2) {
                Ok(m) => match engine.hook_method(m, rawfile2rel_detour as aniimod_mod::NativeFn) {
                    Ok(orig) => {
                        RAWFILE2REL_ORIGINAL.store(orig as u64, Ordering::Release);
                        engine.log("[videodbg] RawFileResIDToRelativePath hooked");
                    }
                    Err(e) => engine.log(&format!("[videodbg] hook failed: {e}")),
                },
                Err(e) => engine.log(&format!("[videodbg] method miss: {e}")),
            }
        } else {
            engine.log("[videodbg] WorldXResourceManager class miss");
        }
        if let Ok(pv) = engine.find_class("_PixVideo.Runtime.dll", "com.vasd.pandora", "PVideoController") {
            if let Ok(m) = engine.find_method(pv, "OpenVideo", 3) {
                match engine.hook_method(m, openvideo_detour as aniimod_mod::NativeFn) {
                    Ok(orig) => {
                        OPENVIDEO_ORIGINAL.store(orig as u64, Ordering::Release);
                        engine.log("[videodbg] PVideoController.OpenVideo hooked");
                    }
                    Err(e) => engine.log(&format!("[videodbg] openvideo hook failed: {e}")),
                }
            }
        }
        Ok(())
    }
}

// il2cpp native ABI: (args..., MethodInfo*) — read managed strings raw.
// Originals stored as addresses (AtomicUsize) and transmuted at call time.
type RawFile2RelFn = unsafe extern "C" fn(*mut core::ffi::c_void, *mut core::ffi::c_void, *const core::ffi::c_void) -> *mut core::ffi::c_void;
type OpenVideoFn = unsafe extern "C" fn(*mut core::ffi::c_void, *mut core::ffi::c_void, bool, bool, *const core::ffi::c_void);
static RAWFILE2REL_ORIGINAL: AtomicU64 = AtomicU64::new(0);
static OPENVIDEO_ORIGINAL: AtomicU64 = AtomicU64::new(0);

/// Minimal Il2CppString read: length@0x10, UTF-16 chars@0x14.
fn il2cpp_str(p: *mut core::ffi::c_void) -> String {
    if p.is_null() {
        return "<null>".into();
    }
    unsafe {
        let base = p as *const u8;
        let len = core::ptr::read_unaligned(base.add(0x10) as *const i32).max(0) as usize;
        let len = len.min(256);
        let mut units = Vec::with_capacity(len);
        for i in 0..len {
            units.push(core::ptr::read_unaligned(base.add(0x14 + i * 2) as *const u16));
        }
        String::from_utf16_lossy(&units)
    }
}

unsafe extern "C" fn rawfile2rel_detour(
    type_ptr: *mut core::ffi::c_void,
    resid_ptr: *mut core::ffi::c_void,
    method: *const core::ffi::c_void,
) -> *mut core::ffi::c_void {
    write_log(&format!(
        "[videodbg] RawFileResIDToRelativePath(type={:?}, resID={:?})",
        il2cpp_str(type_ptr),
        il2cpp_str(resid_ptr)
    ));
    let orig = RAWFILE2REL_ORIGINAL.load(Ordering::Acquire);
    if orig != 0 {
        let f: RawFile2RelFn = unsafe { core::mem::transmute(orig as usize) };
        unsafe { f(type_ptr, resid_ptr, method) }
    } else {
        core::ptr::null_mut()
    }
}

unsafe extern "C" fn openvideo_detour(
    this: *mut core::ffi::c_void,
    resid_ptr: *mut core::ffi::c_void,
    use_alpha: bool,
    is_loop: bool,
    method: *const core::ffi::c_void,
) {
    write_log(&format!(
        "[videodbg] OpenVideo(resID={:?}, alpha={use_alpha}, loop={is_loop})",
        il2cpp_str(resid_ptr)
    ));
    let orig = OPENVIDEO_ORIGINAL.load(Ordering::Acquire);
    if orig != 0 {
        let f: OpenVideoFn = unsafe { core::mem::transmute(orig as usize) };
        unsafe { f(this, resid_ptr, use_alpha, is_loop, method) }
    }
}

#[allow(non_snake_case)]
struct Sample {
    entity: String,
    x: f64,
    y: f64,
    z: f64,
    t: i64,
}

fn guard_placeholder(_f: &Arc<Mutex<Option<FieldHandles>>>, _c: &SendClass, _e: &mut dyn Engine) {}

/// Dump-verified identity chain: BaseComponent.<owner>k__BackingField →
/// Entity.<id>k__BackingField (string). Any miss just falls back to the
/// caller's pointer id — never blocks a sample.
fn entity_id_string(
    engine: &mut dyn Engine,
    component: *mut core::ffi::c_void,
    owner_field: aniimod_mod::FieldPtr,
    id_field: aniimod_mod::FieldPtr,
) -> Option<String> {
    let owner = engine.read_field_object(component, owner_field).ok()?;
    if owner.is_null() {
        return None;
    }
    let id_obj = engine.read_field_object(owner, id_field).ok()?;
    if id_obj.is_null() {
        return None;
    }
    let s = engine.read_string(id_obj);
    if s.is_empty() { None } else { Some(format!("e:{s}")) }
}

/// component -> gameObject -> transform -> get_position (boxed Vector3)
fn read_transform_position(engine: &mut dyn Engine, component: *mut core::ffi::c_void) -> (f32, f32, f32) {
    let step = |r: Result<*mut core::ffi::c_void, String>| r.ok();
    let nan = (f32::NAN, f32::NAN, f32::NAN);
    let Some(comp_class) = engine.find_class("UnityEngine.CoreModule.dll", "UnityEngine", "Component").ok() else { return nan };
    let Some(get_go) = engine.find_method(comp_class, "get_gameObject", 0).ok() else { return nan };
    let Some(go) = step(engine.call_method(get_go, component, &[])) else { return nan };
    if go.is_null() { return nan; }
    let Some(go_class) = engine.find_class("UnityEngine.CoreModule.dll", "UnityEngine", "GameObject").ok() else { return nan };
    let Some(get_tr) = engine.find_method(go_class, "get_transform", 0).ok() else { return nan };
    let Some(tr) = step(engine.call_method(get_tr, go, &[])) else { return nan };
    if tr.is_null() { return nan; }
    let Some(tr_class) = engine.find_class("UnityEngine.CoreModule.dll", "UnityEngine", "Transform").ok() else { return nan };
    let Some(get_pos) = engine.find_method(tr_class, "get_position", 0).ok() else { return nan };
    let Some(boxed) = step(engine.call_method(get_pos, tr, &[])) else { return nan };
    if boxed.is_null() { return nan; }
    unsafe {
        let base = boxed as *const u8;
        let x = core::ptr::read_unaligned(base.add(0x10) as *const f32);
        let y = core::ptr::read_unaligned(base.add(0x14) as *const f32);
        let z = core::ptr::read_unaligned(base.add(0x18) as *const f32);
        (x, y, z)
    }
}

/// Plain-HTTP POST (std TcpStream — Serviimo office is local cleartext).
fn post_telemetry(url: &str, batch: &[Sample]) -> Result<(), String> {
    // url = http://host:port/path
    let rest = url
        .strip_prefix("http://")
        .ok_or_else(|| "only http:// supported".to_string())?;
    let (hostport, path) = rest.split_once('/').ok_or("url missing path")?;
    let (host, port) = match hostport.rsplit_once(':') {
        Some((h, p)) => match p.parse::<u16>() {
            Ok(port) => (h.to_string(), port),
            Err(_) => return Err("bad port".into()),
        },
        None => (hostport.to_string(), 80),
    };

    let mut body = String::from("{\"samples\":[");
    for (i, s) in batch.iter().enumerate() {
        if i > 0 {
            body.push(',');
        }
        body.push_str(&format!(
            "{{\"e\":\"{}\",\"x\":{},\"y\":{},\"z\":{},\"t\":{}}}",
            s.entity, s.x, s.y, s.z, s.t
        ));
    }
    body.push_str("]}");
    let body = body.as_bytes();

    use std::io::{Read, Write};
    let addr = format!("{host}:{port}");
    let mut stream = std::net::TcpStream::connect(&addr).map_err(|e| format!("connect {addr}: {e}"))?;
    stream.set_read_timeout(Some(std::time::Duration::from_secs(3))).ok();
    let req = format!(
        "POST /{} HTTP/1.1\r\nHost: {}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
        path, hostport, body.len()
    );
    stream.write_all(req.as_bytes()).map_err(|e| e.to_string())?;
    stream.write_all(body).map_err(|e| e.to_string())?;
    let mut resp = Vec::new();
    stream.read_to_end(&mut resp).map_err(|e| e.to_string())?;
    let head = String::from_utf8_lossy(&resp[..resp.len().min(64)]).into_owned();
    if head.contains("200") {
        Ok(())
    } else {
        Err(format!("bad response: {}", head.lines().next().unwrap_or("")))
    }
}

// peek_class_of convenience on Engine (klass is the object's first qword)
trait PeekClass {
    fn peek_class_of(&self, obj: *mut core::ffi::c_void) -> ClassPtr;
}
impl PeekClass for &mut dyn Engine {
    fn peek_class_of(&self, obj: *mut core::ffi::c_void) -> ClassPtr {
        if obj.is_null() {
            return core::ptr::null_mut();
        }
        unsafe { core::ptr::read_unaligned(obj as *const ClassPtr) }
    }
}
impl PeekClass for dyn Engine {
    fn peek_class_of(&self, obj: *mut core::ffi::c_void) -> ClassPtr {
        if obj.is_null() {
            return core::ptr::null_mut();
        }
        unsafe { core::ptr::read_unaligned(obj as *const ClassPtr) }
    }
}

fn log_throttled(key: &'static str, msg: &str, min_ms: u64) {
    static LAST: Mutex<Vec<(&'static str, u64)>> = Mutex::new(Vec::new());
    let now = NOW.get().map(|t| t.elapsed().as_millis() as u64).unwrap_or(0);
    let mut last = LAST.lock().unwrap_or_else(|e| e.into_inner());
    let prev = last.iter().find(|(k, _)| *k == key).map(|(_, t)| *t).unwrap_or(0);
    if now - prev >= min_ms {
        if let Some(slot) = last.iter_mut().find(|(k, _)| *k == key) {
            slot.1 = now;
        } else {
            last.push((key, now));
        }
        write_log(msg);
    }
}

fn write_log(msg: &str) {
    // Poll thread logs land directly in the engine's log file; the host
    // doesn't expose logging to detached threads yet (M8 adds it to Engine).
    use std::io::Write;
    let mut path = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| std::path::PathBuf::from("."));
    path.push("AniiModLoader");
    path.push("AniiModRust.log");
    if let Ok(mut f) = std::fs::OpenOptions::new().create(true).append(true).open(path) {
        let _ = writeln!(f, "[capture] {}", msg);
        let _ = f.flush();
    }
}

#[no_mangle]
pub extern "C" fn aniimod_entry() -> *mut dyn Mod {
    Box::into_raw(Box::new(AniiSignal))
}
