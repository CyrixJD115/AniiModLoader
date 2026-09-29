//! aniimod-host — the `version.dll` proxy that IS the mod loader.
//!
//! Boot sequence (docs/ARCHITECTURE.md):
//!   DllMain → install GetProcAddress IAT swap (M2 taps) → spawn engine
//!   thread → wait for GameAssembly.dll → bind il2cpp via the verified
//!   map → wait for the game's OWN il2cpp_init (read the assemblies-array
//!   statics — NEVER call il2cpp_domain_get early: it creates the domain)
//!   → enumerate images → load Mods/*.dll → call on_load → wait for scene
//!   events → M2/M3 markers.
//!
//! M0/M1 proofs (image enumeration, class resolution) stay in the log.

#![cfg(target_family = "windows")]

use std::fs::OpenOptions;
use std::io::Write;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::{Duration, Instant};

use sha2::{Digest, Sha512};

mod proxy;
mod sys;
mod events;
mod mods;
mod console;
mod net;

use std::sync::atomic::AtomicUsize;

use aniimod_ffi::Il2Cpp;
use aniimod_mod::{Engine, Mod, SceneCallback};

/// Offset of the native entry (methodPointer) inside this build's
/// MethodInfo — validated at boot by [validate_method_pointer_offset].
pub(crate) static METHOD_PTR_OFFSET: AtomicUsize = AtomicUsize::new(0);

/// Find which qword slot of a MethodInfo holds the native code pointer by
/// checking that it lands inside GameAssembly's mapped range.
fn validate_method_pointer_offset(il2cpp: &Il2Cpp) -> Option<usize> {
    unsafe {
        let ga = sys::get_module_handle("GameAssembly.dll") as usize;
        if ga == 0 {
            return None;
        }
        // module span from the PE headers (SizeOfImage at nt+0x50)
        let base = ga as *const u8;
        let e_lfanew = core::ptr::read_unaligned(base.add(0x3C) as *const i32);
        // PE32+ optional header: SizeOfImage is at +0x38
        let size = core::ptr::read_unaligned(
            base.offset(e_lfanew as isize).add(24 + 0x38) as *const u32,
        ) as usize;
        let corlib = (il2cpp.get_corlib)();
        let ns = b"System\0";
        let nm = b"String\0";
        let string_class = (il2cpp.class_from_name)(corlib, ns.as_ptr() as *const _, nm.as_ptr() as *const _);
        if string_class.is_null() {
            return None;
        }
        let concat = (il2cpp.class_get_method_from_name)(string_class, b"Concat\0".as_ptr() as *const _, 1);
        if concat.is_null() {
            return None;
        }
        for off in [0usize, 0x10] {
            let p = core::ptr::read_unaligned(concat.add(off) as *const usize);
            if p > ga && p < ga + size {
                METHOD_PTR_OFFSET.store(off, std::sync::atomic::Ordering::Release);
                return Some(off);
            }
        }
    }
    None
}

/// One logger, opened lazily, flush-per-write. Crash-proof by design.
struct Log(PathBuf);

impl Log {
    fn open() -> Log {
        let mut path = sys::exe_dir();
        path.push("AniiModLoader");
        let _ = std::fs::create_dir_all(&path);
        path.push("AniiModRust.log");
        Log(path)
    }

    fn write(&self, line: &str) {
        let stamp = sys::local_time_hms();
        if let Ok(mut f) = OpenOptions::new().create(true).append(true).open(&self.0) {
            let _ = writeln!(f, "[{}] {}", stamp, line);
            let _ = f.flush();
        }
        console::write(&format!("[{}] {}", stamp, line));
    }
}

static LOG: std::sync::OnceLock<Log> = std::sync::OnceLock::new();

/// Log a line from anywhere (engine thread or event taps on game threads).
pub(crate) fn log_line(line: &str) {
    if let Some(log) = LOG.get() {
        log.write(line);
    }
}

static BOOTED: AtomicBool = AtomicBool::new(false);

/// Point Unity's verifying libcurl (and anything else ssl-ish) at the
/// Serviimo CA. The bundle is refreshed by the Serviimo runner into
/// `<game>/AniiModLoader/serviimo-ca.pem` (stable path, stable key —
/// serviimo persists its TLS keypair in serviimo-data/). Env is only set
/// when the file exists, so vanilla-first boots stay untouched.
fn install_serviimo_ca_env() {
    let exe_dir = sys::exe_dir();
    let ca = exe_dir.join("AniiModLoader").join("serviimo-ca.pem");
    if !ca.is_file() {
        return;
    }
    // exe_dir is the game dir in Windows form (Z:\...); backslashes only.
    let path_str: String = ca.to_string_lossy().replace('/', "\\");
    for var in ["CURL_CA_BUNDLE", "SSL_CERT_FILE"] {
        if std::env::var_os(var).is_none() {
            std::env::set_var(var, &path_str);
        }
    }
    log_line(&format!("serviimo CA env: CURL_CA_BUNDLE={path_str}"));
}

#[no_mangle]
pub extern "system" fn DllMain(
    _module: *mut core::ffi::c_void,
    reason: u32,
    _reserved: *mut core::ffi::c_void,
) -> i32 {
    const DLL_PROCESS_ATTACH: u32 = 1;
    const DLL_PROCESS_DETACH: u32 = 0;
    match reason {
        DLL_PROCESS_ATTACH => {
            proxy::SELF_MODULE.store(_module as usize, Ordering::SeqCst);
            if !BOOTED.swap(true, Ordering::SeqCst) {
                // Only the game itself runs the engine.
                if sys::exe_name().to_ascii_lowercase().contains("aniimo.exe") {
                    // Install the M2 taps BEFORE anything else: the IAT swap
                    // must be in place before the engine resolves its first
                    // il2cpp symbol. Loader-lock-safe (pure memory writes).
                    let _ = LOG.set(Log::open());
                    // Unity's libcurl verifies our Serviimo TLS cert against
                    // CURL_CA_BUNDLE (live-proven 2026-09-28: with the env
                    // the error is CN-mismatch, without it "not correctly
                    // signed by a trusted CA"). Steam launches do NOT
                    // inherit the harness env — set it in-process, before
                    // Unity's first HTTPS call. SetEnvironmentVariableA is
                    // loader-lock-safe.
                    install_serviimo_ca_env();
                    let _ = unsafe { events::install(&|s| log_line(s)) };
                    let _ = std::thread::Builder::new()
                        .name("aniimod-engine".into())
                        .stack_size(4 * 1024 * 1024)
                        .spawn(engine_main);
                }
            }
            1
        }
        DLL_PROCESS_DETACH => 1,
        _ => 1,
    }
}

/// Minimal reader for AniiModLoader/network.json (hand-rolled; the host
/// stays dependency-free). Recognized: "redirectXdf": "path",
/// "blockedDomains": ["a.com", "b.org"].
fn read_network_config() -> net::NetConfig {
    let mut path = sys::exe_dir();
    path.push("AniiModLoader");
    path.push("network.json");
    let text = std::fs::read_to_string(&path).unwrap_or_default();

    let grab_strings = |key: &str| -> Vec<String> {
        let key_pat = format!("\"{}\"", key);
        let Some(kpos) = text.find(&key_pat) else { return Vec::new() };
        let rest = &text[kpos + key_pat.len()..];
        let Some(open) = rest.find('[') else { return Vec::new() };
        let Some(close) = rest[open..].find(']') else { return Vec::new() };
        rest[open..open + close + 1]
            .split('"')
            .step_by(2)
            .skip(1)
            .map(|s| s.to_string())
            .collect()
    };

    let redirect_xdf = {
        let key_pat = "\"redirectXdf\"";
        text.find(key_pat)
            .and_then(|kpos| {
                let rest = &text[kpos + key_pat.len()..];
                rest.find('"').map(|q| {
                    let after = &rest[q + 1..];
                    after.find('"').map(|e| after[..e].to_string())
                })
            })
            .flatten()
            .filter(|s| !s.is_empty())
    };

    net::NetConfig {
        redirect_xdf,
        blocked_domains: grab_strings("blockedDomains"),
    }
}

/// M9 update gate: hash the on-disk GameAssembly.dll and refuse to serve a
/// map baked for a different build. A stale map on a rescrambled binary
/// resolves nothing (clean failure) — but a PARTIALLY-correct map could
/// bind a wrong function and crash inside the game's init, so we fail
/// loudly instead and leave instructions for the regen tooling.
fn verify_game_assembly(ga: *mut core::ffi::c_void, log: &Log) -> bool {
    use std::io::Read;

    let Some(path) = sys::module_file_name(ga) else {
        log.write("gate: cannot resolve GameAssembly path — skipping hash check");
        return true;
    };
    let t0 = Instant::now();
    let Ok(mut f) = std::fs::File::open(&path) else {
        log.write(&format!("gate: cannot open {} — skipping hash check", path.display()));
        return true;
    };
    let mut hasher = Sha512::new();
    let mut buf = vec![0u8; 1 << 20];
    let mut read = 0usize;
    loop {
        match f.read(&mut buf) {
            Ok(0) => break,
            Ok(n) => {
                hasher.update(&buf[..n]);
                read += n;
            }
            Err(e) => {
                log.write(&format!("gate: read error at {read}: {e} — skipping hash check"));
                return true;
            }
        }
    }
    let hex: String = hasher
        .finalize()
        .iter()
        .map(|b| format!("{b:02x}"))
        .collect();
    let prefix = &hex[..32];
    log.write(&format!(
        "gate: GameAssembly {read} bytes sha512={prefix}… ({} ms)",
        t0.elapsed().as_millis()
    ));
    if prefix == aniimod_map::EXPECTED_GAME_ASSEMBLY_SHA512_PREFIX {
        true
    } else {
        log.write("FATAL: GameAssembly build mismatch — baked export map is for another build.");
        log.write(&format!(
            "  expected {}  actual {}",
            aniimod_map::EXPECTED_GAME_ASSEMBLY_SHA512_PREFIX,
            prefix
        ));
        log.write("  fix: re-run tools/exportmap (exportscan.py + regen.py) and rebuild the loader;");
        log.write("  see docs/findings.md 'Game updates' — engine will now stand down.");
        // Marker file so the harness/user sees it without reading the log.
        let mut note = sys::exe_dir();
        note.push("AniiModLoader");
        let _ = std::fs::create_dir_all(&note);
        note.push("UPDATE_NEEDED.txt");
        let _ = std::fs::write(
            &note,
            format!(
                "GameAssembly sha512 prefix {prefix} != baked map {}.\n\
                 Regenerate the map (tools/exportmap/regen.py) and rebuild version.dll.\n",
                aniimod_map::EXPECTED_GAME_ASSEMBLY_SHA512_PREFIX
            ),
        );
        false
    }
}

/// Opt-in frida-gadget hosting for dynamic research (nep2 tracing etc.).
/// Enabled by dropping `frida-gadget.dll` + an empty `frida.enabled` marker
/// into AniiModLoader/. The gadget's own `frida-gadget.config` (same dir)
/// controls the interaction mode; ours ships "listen on 127.0.0.1:27042,
/// on_load resume". Attach from Linux with `frida -H 127.0.0.1:27042`.
/// Loaded on the engine thread before the module wait — later than NEP2's
/// DllMain (static import #1 of Aniimo.exe) but before GameAssembly maps
/// and before the VM sweep threads start their scan loops.
fn maybe_host_frida(log: &Log) {
    let mut dir = sys::exe_dir();
    dir.push("AniiModLoader");
    let mut marker = dir.clone();
    marker.push("frida.enabled");
    let mut dll = dir.clone();
    dll.push("frida-gadget.dll");
    if !marker.exists() || !dll.exists() {
        return;
    }
    match sys::load_library(&dll) {
        Ok(()) => log.write("frida: gadget loaded — listen 127.0.0.1:27042 (attach: frida -H 127.0.0.1:27042)"),
        Err(e) => log.write(&format!("frida: gadget load failed: {e}")),
    }
}

fn engine_main() {
    let log = Log::open();
    log.write("aniimod-host v0.4.0 (Rust engine) — boot");
    log.write(&format!("env: {}", sys::env_snapshot()));
    log.write(&format!(
        "map: {} entries for sha512 prefix {}",
        aniimod_map::ExportMap::baked().len(),
        aniimod_map::EXPECTED_GAME_ASSEMBLY_SHA512_PREFIX
    ));
    maybe_host_frida(&log);
    net::init(read_network_config());
    // Live console window (default on; --aniimo.noconsole disables).
    // NOTE (2026-09-27 deadlock): a detached console thread holding win32
    // locks can deadlock il2cpp_init mid-load. It now runs HERE, on the
    // engine thread, before the module wait — a slow wineconsole start
    // only delays the engine, which is waiting for the game anyway.
    if !sys::command_line().contains("--aniimo.noconsole") {
        match console::init("AniiModLoader — Rust Engine") {
            Ok(()) => log.write("console attached"),
            Err(e) => log.write(&format!("console unavailable: {e}")),
        }
    }
    log.write(&format!("pid {} command: {}", sys::pid(), sys::command_line()));

    // ---- M0: module wait, map-driven bind, readiness, enumeration ----
    let ga = match wait_for_module("GameAssembly.dll", Duration::from_secs(180), &log) {
        Some(h) => h,
        None => {
            log.write("FATAL: GameAssembly.dll never appeared — giving up");
            return;
        }
    };
    log.write(&format!("GameAssembly.dll mapped at {:#x}", ga as usize));

    // ---- update gate (M9): refuse to run a map built for another build ----
    if !verify_game_assembly(ga, &log) {
        return;
    }

    // Deterministic M7/M8 arming now that GameAssembly + CRTs are mapped.
    log_line("sweep: starting (SHA gate passed)");
    let _ = unsafe { events::install_address_swaps(&|s| log_line(s)) };

    // Late-module re-sweep poller (POLL design, zero code modification):
    // FPX.dll & friends map AFTER this point and resolve their imports
    // fresh — one sweep pass would never see them (live 2026-09-28: the
    // fpx_config redirect never armed for exactly this reason). Each pass
    // only patches slots still holding the REAL function address, so it
    // is idempotent and cheap. New swaps are logged. Cadence: 250ms for
    // the DLL-load storm (FPX reads fpx_config_pc the instant it maps —
    // a 2s poll lost that race live), then 2s cruise.
    let _ = std::thread::Builder::new()
        .name("aniimod-resweep".into())
        .spawn(|| {
            let mut round = 0u32;
            while round < 900 {
                round += 1;
                let fast = round <= 240; // first minute at 250ms
                std::thread::sleep(std::time::Duration::from_millis(if fast { 250 } else { 2000 }));
                let (n_files, n_dns) = unsafe { events::resweep(false) };
                if n_files + n_dns > 0 {
                    log_line(&format!(
                        "resweep #{round}: {n_files} new file slot(s), {n_dns} resolver slot(s)"
                    ));
                }
            }
        });

    let il2cpp = match unsafe { Il2Cpp::bind(ga as isize) } {
        Ok(api) => api,
        Err(e) => {
            log.write(&format!("FATAL: FFI bind failed: {e}"));
            return;
        }
    };
    log.write("il2cpp API bound via verified export map");

    let (vec_begin, vec_end) = match il2cpp.assemblies_vector_statics() {
        Ok(v) => v,
        Err(e) => {
            log.write(&format!("FATAL: {e}"));
            return;
        }
    };
    let deadline = Instant::now() + Duration::from_secs(240);
    loop {
        if unsafe { Il2Cpp::domain_ready(vec_begin, vec_end) } {
            break;
        }
        if Instant::now() > deadline {
            log.write("FATAL: il2cpp domain never populated — giving up");
            return;
        }
        std::thread::sleep(Duration::from_millis(100));
    }
    let count = {
        let domain = unsafe { (il2cpp.domain_get)() };
        let mut n = 0usize;
        unsafe { (il2cpp.domain_get_assemblies)(domain, &mut n) };
        n
    };
    log.write(&format!("domain populated: {count} assemblies"));

    let names = unsafe { il2cpp.image_names() };
    log.write(&format!("images map: {} entries", names.len()));
    // build the process-lifetime image table (name, image ptr)
    {
        let domain = unsafe { (il2cpp.domain_get)() };
        let mut n = 0usize;
        let assemblies = unsafe { (il2cpp.domain_get_assemblies)(domain, &mut n) };
        let mut v = Vec::with_capacity(n);
        for i in 0..n {
            unsafe {
                let image = (il2cpp.assembly_get_image)(*assemblies.add(i));
                let name = core::ffi::CStr::from_ptr((il2cpp.image_get_name)(image))
                    .to_string_lossy()
                    .into_owned();
                v.push((name, image));
            }
        }
        let _ = mods::IMAGES.set(mods::ImageTable(v));
    }

    // ---- M1: class-resolution proof ----
    unsafe {
        let corlib = (il2cpp.get_corlib)();
        let corlib_name = core::ffi::CStr::from_ptr((il2cpp.image_get_name)(corlib))
            .to_string_lossy()
            .into_owned();
        log.write(&format!("M1 get_corlib -> image named {corlib_name:?}"));
        let cstr = |s: &str| {
            let mut v = s.as_bytes().to_vec();
            v.push(0);
            v
        };
        let ns = cstr("System");
        let nm = cstr("String");
        let string_class = (il2cpp.class_from_name)(
            corlib,
            ns.as_ptr() as *const core::ffi::c_char,
            nm.as_ptr() as *const core::ffi::c_char,
        );
        if string_class.is_null() {
            log.write("M1 FAIL: System.String not resolvable");
        } else {
            let got = core::ffi::CStr::from_ptr((il2cpp.class_get_name)(string_class))
                .to_string_lossy()
                .into_owned();
            log.write(&format!("M1 class_from_name(System.String) -> {got:?}"));
        }
    }
    log.write("M0+M1 COMPLETE");

    // ---- M5 prep: attach this thread (GC-object creation needs it) and
    // validate the MethodInfo methodPointer offset ----
    unsafe { (il2cpp.thread_attach)((il2cpp.domain_get)()) };
    match validate_method_pointer_offset(&il2cpp) {
        Some(off) => log.write(&format!("methodPointer offset validated: MethodInfo+{off:#x}")),
        None => log.write("WARN: methodPointer offset NOT validated — hooking disabled"),
    }

    // ---- M3: load mods ----
    let images = mods::IMAGES.get().expect("images table set above");
    let loaded = mods::load_all(&|s: &str| log.write(s), &il2cpp, images);
    log.write(&format!("M3: {} mod(s) loaded", loaded.len()));
    if !loaded.is_empty() {
        log.write("M3 COMPLETE");
    }

    // ---- M2: wait for the first scene-change tap, then game_ready pass ----
    let deadline = Instant::now() + Duration::from_secs(240);
    loop {
        if events::scene_count() > 0 {
            log.write(&format!(
                "M2 COMPLETE — scene tap live after {} events; init_seen={} invoke_seen={}",
                events::scene_count(),
                events::init_seen(),
                events::invoke_seen()
            ));
            break;
        }
        if Instant::now() > deadline {
            log.write(&format!(
                "M2 TIMEOUT (no scene event in 240s; init_seen={} invoke_seen={})",
                events::init_seen(),
                events::invoke_seen()
            ));
            break;
        }
        std::thread::sleep(Duration::from_millis(250));
    }

    if events::scene_count() > 0 && !loaded.is_empty() {
        mods::game_ready_all(&|s: &str| log.write(s), &loaded, &il2cpp, images);
        log.write("game_ready pass complete");
    }

    log.write("engine_main finished — taps stay live");
}

fn wait_for_module(name: &str, timeout: Duration, log: &Log) -> Option<*mut core::ffi::c_void> {
    let deadline = Instant::now() + timeout;
    loop {
        let h = unsafe { sys::get_module_handle(name) };
        if !h.is_null() {
            return Some(h);
        }
        if Instant::now() > deadline {
            return None;
        }
        std::thread::sleep(Duration::from_millis(50));
    }
}
