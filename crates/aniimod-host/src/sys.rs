//! Raw kernel32 FFI — deliberately dependency-free so the cross-build needs
//! nothing beyond Rust std + mingw.

#![allow(non_snake_case)]

use std::ffi::OsString;
use std::os::windows::ffi::{OsStrExt, OsStringExt};
use std::path::PathBuf;

#[link(name = "kernel32")]
extern "system" {
    fn GetModuleHandleA(name: *const u8) -> *mut core::ffi::c_void;
    fn GetModuleFileNameW(module: *mut core::ffi::c_void, path: *mut u16, size: u32) -> u32;
    fn GetProcAddress(module: isize, name: *const u8) -> isize;
    fn GetCurrentProcessId() -> u32;
    fn GetCommandLineA() -> *const u8;
    fn GetEnvironmentVariableA(name: *const u8, buf: *mut u8, size: u32) -> u32;
    fn LoadLibraryExW(path: *const u16, reserved: *mut core::ffi::c_void, flags: u32) -> *mut core::ffi::c_void;
    #[cfg_attr(windows, allow(dead_code))]
    fn GetLocalTime(st: *mut SYSTEMTIME);
}

/// LOAD_WITH_ALTERED_SEARCH_PATH: the loaded DLL's dependencies (and its
/// .config) resolve from its own directory, not the exe's.
const LOAD_WITH_ALTERED_SEARCH_PATH: u32 = 0x8;

/// Load a plugin DLL by path (used for opt-in frida-gadget hosting).
pub fn load_library(path: &std::path::Path) -> Result<(), String> {
    let wide: Vec<u16> = path
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .collect();
    let h = unsafe { LoadLibraryExW(wide.as_ptr(), core::ptr::null_mut(), LOAD_WITH_ALTERED_SEARCH_PATH) };
    if h.is_null() {
        Err(format!("LoadLibraryExW({}) failed", path.display()))
    } else {
        Ok(())
    }
}

#[repr(C)]
struct SYSTEMTIME {
    wYear: u16,
    wMonth: u16,
    wDayOfWeek: u16,
    wDay: u16,
    wHour: u16,
    wMinute: u16,
    wSecond: u16,
    wMilliseconds: u16,
}

pub fn get_module_handle(name: &str) -> *mut core::ffi::c_void {
    let mut buf = name.as_bytes().to_vec();
    buf.push(0);
    unsafe { GetModuleHandleA(buf.as_ptr()) }
}

pub fn get_proc_address(module: isize, name: *const u8) -> isize {
    unsafe { GetProcAddress(module, name) }
}

/// File name of the main executable (checked before the engine starts).
pub fn exe_name() -> String {
    let mut buf = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(core::ptr::null_mut(), buf.as_mut_ptr(), 1024) };
    let full: PathBuf = OsString::from_wide(&buf[..len as usize]).into();
    full.file_name().map(|f| f.to_string_lossy().into_owned()).unwrap_or_default()
}

/// Directory containing Aniimo.exe (our version.dll sits next to it).
pub fn exe_dir() -> PathBuf {
    let mut buf = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(core::ptr::null_mut(), buf.as_mut_ptr(), 1024) };
    let full: PathBuf = OsString::from_wide(&buf[..len as usize]).into();
    full.parent().map(|p| p.to_path_buf()).unwrap_or_default()
}

pub fn pid() -> u32 {
    unsafe { GetCurrentProcessId() }
}

pub fn command_line() -> String {
    unsafe {
        let p = GetCommandLineA();
        if p.is_null() {
            return String::new();
        }
        let mut len = 0usize;
        while *p.add(len) != 0 {
            len += 1;
        }
        String::from_utf8_lossy(core::slice::from_raw_parts(p, len)).into_owned()
    }
}

pub fn local_time_hms() -> String {
    let mut st = SYSTEMTIME {
        wYear: 0,
        wMonth: 0,
        wDayOfWeek: 0,
        wDay: 0,
        wHour: 0,
        wMinute: 0,
        wSecond: 0,
        wMilliseconds: 0,
    };
    unsafe { GetLocalTime(&mut st) };
    format!("{:02}:{:02}:{:02}.{:03}", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds)
}

/// On-disk path of a loaded module (used to hash GameAssembly.dll).
pub fn module_file_name(module: *mut core::ffi::c_void) -> Option<PathBuf> {
    let mut buf = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(module, buf.as_mut_ptr(), 1024) };
    if len == 0 {
        return None;
    }
    Some(OsString::from_wide(&buf[..len as usize]).into())
}

pub fn env_var(name: &str) -> Option<String> {
    let mut n = name.as_bytes().to_vec();
    n.push(0);
    let mut buf = [0u8; 512];
    let len = unsafe { GetEnvironmentVariableA(n.as_ptr(), buf.as_mut_ptr(), 512) };
    if len == 0 || len as usize >= 512 {
        return None;
    }
    Some(String::from_utf8_lossy(&buf[..len as usize]).into_owned())
}

/// Wine build string via ntdll!wine_get_version — absent on native Windows.
pub fn wine_version() -> String {
    let ntdll = get_module_handle("ntdll.dll");
    if ntdll.is_null() {
        return "no ntdll?".into();
    }
    let mut name = b"wine_get_version\0";
    let p = unsafe { GetProcAddress(ntdll as isize, name.as_ptr()) };
    if p == 0 {
        return "native windows (no wine_get_version)".into();
    }
    let f: unsafe extern "system" fn() -> *const u8 = unsafe { core::mem::transmute(p) };
    let v = unsafe { f() };
    if v.is_null() {
        return "wine_get_version returned null".into();
    }
    unsafe {
        let mut len = 0usize;
        while *v.add(len) != 0 {
            len += 1;
        }
        String::from_utf8_lossy(core::slice::from_raw_parts(v, len)).into_owned()
    }
}

/// One-line environment snapshot for the boot log (research context).
pub fn env_snapshot() -> String {
    let mut parts = vec![format!("wine={}", wine_version())];
    for var in ["WINEPREFIX", "WINEESYNC", "WINEFSYNC", "SteamGameId", "SteamAppId"] {
        if let Some(v) = env_var(var) {
            parts.push(format!("{var}={v}"));
        }
    }
    parts.join(" ")
}
