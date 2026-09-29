//! The console window (user-facing live log, like the C# loader had).
//!
//! AllocConsole attaches a fresh console to this GUI process; every log
//! line is then mirrored to it with a color picked by content. Default ON;
//! `--aniimo.noconsole` on the game command line disables it.

#![cfg(target_family = "windows")]

use std::sync::atomic::{AtomicUsize, Ordering};

#[link(name = "kernel32")]
extern "system" {
    fn AllocConsole() -> i32;
    fn CreateFileW(
        name: *const u16,
        access: u32,
        share: u32,
        security: *mut core::ffi::c_void,
        disposition: u32,
        flags: u32,
        template: isize,
    ) -> isize;
    fn SetConsoleTitleW(title: *const u16) -> i32;
    fn SetConsoleTextAttribute(handle: isize, attr: u16) -> i32;
    fn WriteConsoleW(
        handle: isize,
        buffer: *const u16,
        chars: u32,
        written: *mut u32,
        reserved: *mut core::ffi::c_void,
    ) -> i32;
}

const GENERIC_WRITE: u32 = 0x4000_0000;
const FILE_SHARE_WRITE: u32 = 0x2;
const OPEN_EXISTING: u32 = 0x3;
const INVALID_HANDLE: isize = -1;

const GRAY: u16 = 0x8;
const WHITE: u16 = 0xF;
const GREEN: u16 = 0xA;
const YELLOW: u16 = 0xE;
const RED: u16 = 0xC;
const CYAN: u16 = 0xB;
const MAGENTA: u16 = 0xD;

static HANDLE: AtomicUsize = AtomicUsize::new(0);

/// Attach the console. Returns false when one couldn't be created (e.g. a
/// console already exists or the session has no window station) — logging
/// then stays file-only.
pub fn init(title: &str) -> Result<(), &'static str> {
    unsafe {
        if AllocConsole() == 0 {
            return Err("AllocConsole failed");
        }
        let wide: Vec<u16> = title.encode_utf16().chain([0]).collect();
        SetConsoleTitleW(wide.as_ptr());
        // Open the fresh console's output buffer directly — GetStdHandle is
        // unreliable under wine right after AllocConsole.
        let conout: Vec<u16> = "CONOUT$\0".encode_utf16().collect();
        let h = CreateFileW(
            conout.as_ptr(),
            GENERIC_WRITE,
            FILE_SHARE_WRITE,
            core::ptr::null_mut(),
            OPEN_EXISTING,
            0,
            0,
        );
        if h == 0 || h == INVALID_HANDLE {
            return Err("CONOUT$ open failed");
        }
        HANDLE.store(h as usize, Ordering::Release);
        Ok(())
    }
}

fn pick_color(line: &str) -> u16 {
    if line.contains("FATAL") {
        RED
    } else if line.contains("COMPLETE") {
        GREEN
    } else if line.contains("scene event") {
        YELLOW
    } else if line.starts_with('[') {
        CYAN // mod-owned lines
    } else if line.contains("detour") || line.contains("tap") || line.starts_with("M2") {
        MAGENTA
    } else if line.starts_with("aniimod-host") || line.contains("pid ") {
        WHITE
    } else {
        GRAY
    }
}

/// Mirror one log line to the console (thread-safe enough: the console
/// subsystem serializes writes).
pub fn write(line: &str) {
    let h = HANDLE.load(Ordering::Acquire) as isize;
    if h == 0 {
        return;
    }
    let text = format!("{}\r\n", line);
    let wide: Vec<u16> = text.encode_utf16().collect();
    unsafe {
        SetConsoleTextAttribute(h, pick_color(line));
        let mut written = 0u32;
        WriteConsoleW(h, wide.as_ptr(), wide.len() as u32, &mut written, core::ptr::null_mut());
        SetConsoleTextAttribute(h, GRAY);
    }
}
