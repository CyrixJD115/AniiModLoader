//! aniimod-hook — pointer-level hooking, safe-by-default.
//!
//! The proven mechanism (C# bootstrap + Rust M2, 2026-09-27): patch
//! **UnityPlayer.dll's** import entry for `GetProcAddress` (its KERNEL32
//! descriptor is resolved before our version.dll DllMain runs, so the
//! patch from DllMain is race-free) so the engine's symbol lookups pass
//! through our detour first. We hand the engine our own
//! functions when it asks for `il2cpp_init` / `il2cpp_runtime_invoke`'s
//! scrambled names. Zero GameAssembly code pages are touched — this exact
//! approach survived every boot incl. anti-cheat observation.
//!
//! Inline trampolines exist as a future opt-in (`--aniimod.trampolines`)
//! for call sites a pointer swap can't reach.

#![cfg(target_family = "windows")]
#![allow(clippy::missing_safety_doc)]

#[link(name = "kernel32")]
extern "system" {
    fn VirtualProtect(
        addr: *mut core::ffi::c_void,
        size: usize,
        new_protect: u32,
        old_protect: *mut u32,
    ) -> i32;
    fn VirtualAlloc(
        addr: *mut core::ffi::c_void,
        size: usize,
        alloc_type: u32,
        protect: u32,
    ) -> isize;
    fn FlushInstructionCache(proc: isize, base: *const core::ffi::c_void, size: usize) -> i32;
    fn GetCurrentProcess() -> isize;
}

const PAGE_READWRITE: u32 = 0x04;
const MEM_COMMIT: u32 = 0x1000;
const MEM_RESERVE: u32 = 0x2000;
const PAGE_EXECUTE_READWRITE: u32 = 0x40;

#[derive(Debug)]
pub enum HookError {
    /// Could not parse the main executable's import table.
    BadPe(&'static str),
    /// The dll/function import entry was not found.
    NoImportEntry,
    /// Prologue not in the relocation whitelist; carries the first 16 bytes.
    NotRelocatable(String),
}

/// Swap one entry in the MAIN EXECUTABLE's import table.
///
/// Returns the original pointer stored in the IAT slot (call it to reach
/// the real function). DLL_PROCESS_ATTACH-safe: pure memory writes via
/// VirtualProtect, no loader interaction, no allocation.
///
/// `dll` is matched case-insensitively (imports are recorded as
/// "KERNEL32.dll" etc).
pub unsafe fn swap_module_import(
    module: isize,
    dll: &str,
    func: &str,
    new_ptr: *const core::ffi::c_void,
) -> Result<*mut core::ffi::c_void, HookError> {
    if module == 0 {
        return Err(HookError::BadPe("module not loaded"));
    }
    let base = module as *const u8;

    // --- minimal PE32+ walk ---
    let e_lfanew = read_i32(base, 0x3C).ok_or(HookError::BadPe("e_lfanew"))?;
    let nt = base.offset(e_lfanew as isize);
    if read_u16(nt, 0) != Some(0x4550) {
        return Err(HookError::BadPe("PE signature"));
    }
    let opt = nt.add(24);
    if read_u16(opt, 0) != Some(0x20B) {
        return Err(HookError::BadPe("not PE32+"));
    }
    let import_rva = read_u32(opt, 112 + 8).ok_or(HookError::BadPe("import dir"))?; // data dir [1]
    if import_rva == 0 {
        return Err(HookError::NoImportEntry);
    }

    let mut desc = base.offset(import_rva as isize);
    loop {
        let ilt = read_u32(desc, 0).unwrap_or(0); // OriginalFirstThunk
        let name_rva = read_u32(desc, 12).unwrap_or(0);
        let iat_rva = read_u32(desc, 16).unwrap_or(0);
        if name_rva == 0 && iat_rva == 0 {
            return Err(HookError::NoImportEntry); // end of descriptors
        }
        let dll_name = cstr_at(base, name_rva);
        if dll_eq(dll_name, dll) {
            // walk the INT (fallback: the IAT itself) in parallel with the IAT
            let walk_rva = if ilt != 0 { ilt } else { iat_rva };
            let mut i = 0usize;
            loop {
                let thunk = read_u64(base, walk_rva + (i as u32) * 8).unwrap_or(0);
                if thunk == 0 {
                    break; // not in this dll
                }
                if thunk & (1 << 63) == 0 {
                    // imported by name: thunk RVA -> IMAGE_IMPORT_BY_NAME (hint + name)
                    let fname = base.offset(thunk as isize).add(2);
                    if bytes_eq_z(fname, func.as_bytes()) {
                        let slot = base.offset((iat_rva + (i as u32) * 8) as isize);
                        let mut old = 0u32;
                        if VirtualProtect(slot as *mut core::ffi::c_void, 8, PAGE_READWRITE, &mut old) == 0 {
                            return Err(HookError::BadPe("VirtualProtect failed"));
                        }
                        let orig = *(slot as *mut *mut core::ffi::c_void);
                        *(slot as *mut *const core::ffi::c_void) = new_ptr;
                        let _ = VirtualProtect(slot as *mut core::ffi::c_void, 8, old, &mut old);
                        return Ok(orig);
                    }
                }
                i += 1;
            }
        }
        desc = desc.add(20); // sizeof IMAGE_IMPORT_DESCRIPTOR
    }
}

fn read_u16(base: *const u8, off: usize) -> Option<u16> {
    unsafe { base.add(off).cast::<u16>().as_ref().map(|v| v.to_le()) }
}

/// Swap every IAT slot in `module` that currently holds `current` (the
/// resolved address of an import from `dll`) to `new_ptr`. Returns the
/// number of slots swapped. Unlike [`swap_module_import`] this matches the
/// RESOLVED ADDRESS, so it covers ordinal imports (modern CRT/DirectX-style
/// binaries import CreateFileW & co. by ordinal, invisible to name walks).
/// Data-page writes only — anti-tamper-safe.
pub unsafe fn swap_module_import_by_address(
    module: isize,
    dll: &str,
    current: usize,
    new_ptr: *const core::ffi::c_void,
) -> usize {
    let mut swapped = 0usize;
    if module == 0 || current == 0 {
        return 0;
    }
    let base = module as *const u8;
    let Some(e_lfanew) = read_i32(base, 0x3C) else { return 0 };
    let nt = base.offset(e_lfanew as isize);
    if read_u16(nt, 0) != Some(0x4550) {
        return 0;
    }
    let opt = nt.add(24);
    if read_u16(opt, 0) != Some(0x20B) {
        return 0;
    }
    let import_rva = match read_u32(opt, 112 + 8) {
        Some(r) if r != 0 => r,
        _ => return 0,
    };
    let mut desc = base.offset(import_rva as isize);
    loop {
        let name_rva = read_u32(desc, 12).unwrap_or(0);
        let iat_rva = read_u32(desc, 16).unwrap_or(0);
        if name_rva == 0 && iat_rva == 0 {
            return swapped;
        }
        if dll_eq(cstr_at(base, name_rva), dll) {
            let mut i = 0u32;
            loop {
                let slot = base.offset((iat_rva + i * 8) as isize);
                let Some(val) = (unsafe { (slot as *const usize).as_ref() }) else { break };
                let val = val.to_le();
                if val == 0 {
                    break;
                }
                if val == current {
                    let mut old = 0u32;
                    if VirtualProtect(slot as *mut core::ffi::c_void, 8, PAGE_READWRITE, &mut old) != 0 {
                        unsafe { *(slot as *mut *const core::ffi::c_void) = new_ptr };
                        let _ = VirtualProtect(slot as *mut core::ffi::c_void, 8, old, &mut old);
                        swapped += 1;
                    }
                }
                i += 1;
            }
        }
        desc = desc.add(20);
    }
}

fn read_u32(base: *const u8, off: usize) -> Option<u32> {
    unsafe { base.add(off).cast::<u32>().as_ref().map(|v| v.to_le()) }
}

fn read_i32(base: *const u8, off: usize) -> Option<i32> {
    unsafe { base.add(off).cast::<i32>().as_ref().map(|v| v.to_le()) }
}

fn read_u64(base: *const u8, off: u32) -> Option<u64> {
    unsafe {
        base.offset(off as isize)
            .cast::<u64>()
            .as_ref()
            .map(|v| v.to_le())
    }
}

fn cstr_at(base: *const u8, rva: u32) -> *const u8 {
    if rva == 0 {
        return core::ptr::null();
    }
    unsafe { base.offset(rva as isize) }
}

fn dll_eq(name: *const u8, want: &str) -> bool {
    if name.is_null() {
        return false;
    }
    let want = want.as_bytes();
    unsafe {
        let mut i = 0;
        loop {
            let a = *name.add(i);
            let b = if i < want.len() { want[i] } else { b'\0' };
            if a.to_ascii_uppercase() != b.to_ascii_uppercase() {
                return false;
            }
            if a == 0 {
                return true;
            }
            i += 1;
        }
    }
}

/// Compare a C string against a needle (no allocation; early-out on first
/// mismatching byte).
pub fn bytes_eq_z(mut s: *const u8, needle: &[u8]) -> bool {
    if s.is_null() {
        return false;
    }
    unsafe {
        for &want in needle {
            if *s != want {
                return false;
            }
            s = s.add(1);
        }
        *s == 0
    }
}


// ---------------------------------------------------------------------------
// M6: method-body trampoline (catches the game's own direct calls, unlike
// slot swaps which only intercept runtime_invoke/reflection).
//
// Patch: first N bytes of the method body -> `mov rax, detour; jmp rax`
// (12 bytes, padded to N with int3). Trampoline: a copy of the original N
// bytes + `mov rax, body+N; jmp rax`. Calling the trampoline runs the
// displaced prologue then re-enters the body — a full, race-free original.
//
// Safety gate: only relocate whitelisted prologue forms (push reg,
// sub rsp,imm8, reg-reg mov) with NO rip-relative or relative-branch
// instructions. Anything else -> refuse, never guess.

/// Compute how many prologue bytes can be safely relocated (>= 14 needed).
/// Returns None when a non-whitelisted instruction appears first.
unsafe fn safe_relocate_len(body: *const u8, need: usize) -> Option<usize> {
    let mut off = 0usize;
    while off < need {
        let b = *body.add(off);
        let len = match b {
            // 50-57: push reg
            0x50..=0x57 => 1,
            // 41 50-57 / 40 50-57: rex push reg
            0x41 | 0x40 if *body.add(off + 1) & 0xF8 == 0x50 => 2,
            // 48 83 EC imm8 / 49 83 EC imm8: sub rsp, imm8
            0x48 | 0x49 if *body.add(off + 1) == 0x83 && *body.add(off + 2) == 0xEC => 4,
            // 48 81 EC imm32 / 49 81 EC imm32: sub rsp, imm32
            0x48 | 0x49 if *body.add(off + 1) == 0x81 && *body.add(off + 2) == 0xEC => 7,
            // 48 89 / 48 8B / 4C 89 / 4C 8B / 49 8B / 49 89:
            //   mod=11 (reg-reg) = 3 bytes; mod=01 with rm!=101 (disp8, no rip) = 4
            0x48 | 0x4C | 0x49
                if matches!(*body.add(off + 1), 0x89 | 0x8B | 0x8D) =>
            {
                let modrm = *body.add(off + 2);
                match modrm >> 6 {
                    3 => 3,
                    1 if modrm & 0x07 != 5 => 4, // disp8, SIB-free base != rip
                    _ => return None,            // rip-rel, disp32, or SIB — refuse
                }
            }
            // 44 31 mod11: xor r,r
            0x44 if *body.add(off + 1) == 0x31 && (*body.add(off + 2) & 0xC7) == 0xC0 => 3,
            _ => return None,
        };
        off += len;
    }
    Some(off)
}

/// Patch a method body so all callers land in `detour`. Returns the
/// trampoline (call it to run the original body).
///
/// # Safety
/// `body` must be the native entry of an il2cpp method (methodPointer slot).
pub unsafe fn hook_body(
    body: *mut core::ffi::c_void,
    detour: *const core::ffi::c_void,
) -> Result<*mut core::ffi::c_void, HookError> {
    let body_c = body as *const u8;
    let body = body as *mut u8;
    let len = match safe_relocate_len(body_c, 14) {
        Some(l) => l,
        None => {
            let mut hex = String::new();
            for i in 0..16 {
                hex.push_str(&format!("{:02x}-", unsafe { *body_c.add(i) }));
            }
            return Err(HookError::NotRelocatable(hex));
        }
    };

    // trampoline: relocated prologue + mov rax, body+len; jmp rax
    let tramp = VirtualAlloc(
        core::ptr::null_mut(),
        len + 16,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE,
    );
    if tramp == 0 {
        return Err(HookError::BadPe("VirtualAlloc trampoline"));
    }
    let tramp = tramp as *mut u8;
    core::ptr::copy_nonoverlapping(body, tramp, len);
    let mut o = tramp.add(len);
    *o = 0x48;
    *o.add(1) = 0xB8;
    (body.add(len) as usize).to_le_bytes().iter().enumerate().for_each(|(i, b)| *o.add(2 + i) = *b);
    *o.add(10) = 0xFF;
    *o.add(11) = 0xE0;

    // patch: mov rax, detour; jmp rax (+ int3 pad to len)
    let mut old = 0u32;
    if VirtualProtect(
        body as *mut core::ffi::c_void,
        len,
        PAGE_READWRITE,
        &mut old,
    ) == 0
    {
        return Err(HookError::BadPe("VirtualProtect body"));
    }
    let detour_addr = detour as usize;
    let patch: [u8; 12] = {
        let mut p = [0u8; 12];
        p[0] = 0x48;
        p[1] = 0xB8;
        p[2..10].copy_from_slice(&detour_addr.to_le_bytes());
        p[10] = 0xFF;
        p[11] = 0xE0;
        p
    };
    core::ptr::copy_nonoverlapping(patch.as_ptr(), body as *mut u8, 12);
    for i in 12..len {
        *body.add(i) = 0xCC; // int3 — must never execute
    }
    let _ = VirtualProtect(body as *mut core::ffi::c_void, len, old, &mut old);
    FlushInstructionCache(GetCurrentProcess(), body as *const core::ffi::c_void, len);
    Ok(tramp as *mut core::ffi::c_void)
}

/// Restore a body previously patched by [`hook_body`].
///
/// # Safety
/// `body`/`len` must match a prior `hook_body`; call only when no thread
/// can be inside the patched range.
pub unsafe fn restore_body(body: *mut core::ffi::c_void, original: &[u8]) {
    let mut old = 0u32;
    if VirtualProtect(body, original.len(), PAGE_READWRITE, &mut old) != 0 {
        core::ptr::copy_nonoverlapping(original.as_ptr(), body as *mut u8, original.len());
        let _ = VirtualProtect(body, original.len(), old, &mut old);
        FlushInstructionCache(GetCurrentProcess(), body, original.len());
    }
}
