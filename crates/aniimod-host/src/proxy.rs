//! version.dll API surface.
//!
//! UnityPlayer imports VERSION.dll, so our proxy must satisfy those imports.
//! Strategy: lazy forwarding — the first call to any forwarded export loads
//! the REAL system version.dll via `LoadLibraryExW(LOAD_LIBRARY_SEARCH_SYSTEM32)`
//! (which bypasses the application directory, avoiding the self-reference
//! trap), resolves the true function, and caches it. If the system library
//! cannot be loaded we return benign failure values; Unity titles never
//! actually depend on version-query results to boot.

#![allow(non_snake_case)]

use std::sync::atomic::{AtomicUsize, Ordering};

#[link(name = "kernel32")]
extern "system" {
    fn LoadLibraryExW(name: *const u16, file: *const core::ffi::c_void, flags: u32) -> isize;
    fn GetProcAddress(module: isize, name: *const u8) -> isize;
}

const LOAD_LIBRARY_SEARCH_SYSTEM32: u32 = 0x800;

static REAL_VERSION: AtomicUsize = AtomicUsize::new(0);
/// set to 1 when forwarding is known-broken (self-load detected) so we
/// never recurse into our own stubs
static FORWARD_DEAD: AtomicUsize = AtomicUsize::new(0);
/// our own module handle, set from DllMain
pub static SELF_MODULE: AtomicUsize = AtomicUsize::new(0);

fn real_module() -> isize {
    if FORWARD_DEAD.load(Ordering::Acquire) == 1 {
        return 0;
    }
    match REAL_VERSION.load(Ordering::Acquire) {
        0 => {
            let name: Vec<u16> = "version.dll\0".encode_utf16().collect();
            let h = unsafe { LoadLibraryExW(name.as_ptr(), core::ptr::null(), LOAD_LIBRARY_SEARCH_SYSTEM32) };
            // WINE may ignore LOAD_LIBRARY_SEARCH_SYSTEM32 and hand us back
            // our own module by name — forwarding through it would recurse
            // forever (the stack-overflow crash we saw). Detect and disable.
            if h as usize == SELF_MODULE.load(Ordering::Acquire) {
                FORWARD_DEAD.store(1, Ordering::Release);
                return 0;
            }
            REAL_VERSION.store(h as usize, Ordering::Release);
            h
        }
        cached => cached as isize,
    }
}

unsafe fn real_export(name: &[u8]) -> isize {
    let m = real_module();
    if m == 0 {
        return 0;
    }
    let mut buf = name.to_vec();
    buf.push(0);
    GetProcAddress(m, buf.as_ptr())
}

macro_rules! forward {
    ($name:ident ($($arg:ident: $ty:ty),*) -> $ret:ty = $default:expr) => {
        #[no_mangle]
        pub extern "system" fn $name($($arg: $ty),*) -> $ret {
            unsafe {
                let real = real_export(stringify!($name).as_bytes());
                if real != 0 {
                    let f: unsafe extern "system" fn($($ty),*) -> $ret =
                        core::mem::transmute(real);
                    return f($($arg),*);
                }
            }
            $default
        }
    };
}

type LPCSTR = *const u8;
type LPCWSTR = *const u16;
type DWORD = u32;
type BOOL = i32;
type LPVOID = *mut core::ffi::c_void;
type LPCVOID = *const core::ffi::c_void;
type UINT = u32;

forward!(GetFileVersionInfoA(lpwstrfilename: LPCSTR, dwhandle: DWORD, dwlen: DWORD, lpdata: LPVOID) -> BOOL = 0);
forward!(GetFileVersionInfoW(lpwstrfilename: LPCWSTR, dwhandle: DWORD, dwlen: DWORD, lpdata: LPVOID) -> BOOL = 0);
forward!(GetFileVersionInfoExA(flags: DWORD, filename: LPCSTR, handle: DWORD, len: DWORD, data: LPVOID) -> BOOL = 0);
forward!(GetFileVersionInfoExW(flags: DWORD, filename: LPCWSTR, handle: DWORD, len: DWORD, data: LPVOID) -> BOOL = 0);
forward!(GetFileVersionInfoSizeA(filename: LPCSTR, handle: *mut DWORD) -> DWORD = 0);
forward!(GetFileVersionInfoSizeW(filename: LPCWSTR, handle: *mut DWORD) -> DWORD = 0);
forward!(GetFileVersionInfoSizeExA(flags: DWORD, filename: LPCSTR, handle: *mut DWORD) -> DWORD = 0);
forward!(GetFileVersionInfoSizeExW(flags: DWORD, filename: LPCWSTR, handle: *mut DWORD) -> DWORD = 0);
forward!(VerQueryValueA(block: LPCVOID, subblock: LPCSTR, buffer: *mut LPVOID, len: *mut UINT) -> BOOL = 0);
forward!(VerQueryValueW(block: LPCVOID, subblock: LPCWSTR, buffer: *mut LPVOID, len: *mut UINT) -> BOOL = 0);
forward!(VerLanguageNameA(lang: DWORD, langname: LPCSTR, bufsize: DWORD) -> DWORD = 0);
forward!(VerLanguageNameW(lang: DWORD, langname: LPCWSTR, bufsize: DWORD) -> DWORD = 0);
forward!(VerFindFileA(flags: DWORD, filename: LPCSTR, windir: LPCSTR, appdir: LPCSTR, curdir: LPCSTR, tmpfile: LPCSTR) -> DWORD = 0);
forward!(VerFindFileW(flags: DWORD, filename: LPCWSTR, windir: LPCWSTR, appdir: LPCWSTR, curdir: LPCWSTR, tmpfile: LPCWSTR) -> DWORD = 0);
forward!(VerInstallFileA(flags: DWORD, src: LPCSTR, dest: LPCSTR, srcdir: LPCSTR, destdir: LPCSTR, curdir: LPCSTR, tmpfile: LPCSTR, tmpfilelen: *mut UINT) -> DWORD = 0);
forward!(VerInstallFileW(flags: DWORD, src: LPCWSTR, dest: LPCWSTR, srcdir: LPCWSTR, destdir: LPCWSTR, curdir: LPCWSTR, tmpfile: LPCWSTR, tmpfilelen: *mut UINT) -> DWORD = 0);
