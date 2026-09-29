# Encoded findings (2026-09-26/27 deep-dive)

Hard facts the Rust engine builds on. Everything here was verified against
GameAssembly build 25525975 (SHA-512 FAA2D225…60CD0, Unity 2022.3.62f1).

## Export table

- 390 named exports. ALL `il2cpp_*` names scrambled to `i0[a-z0-9]+`
  (e.g. `il2cpp_init = i084v6jzbst`). ~145 real-named exports survive:
  `mono_*` compat layer, `Brotli*`, `SystemNative_*`, etc.
- Scrambled exports occupy EAT ordinals 25–269 **in alphabetical order of
  the stock il2cpp API name**. This is the master key for regeneration.
- The MSVC linker **ICF-folded identical bodies**: all `mov rax,[rcx]; ret`
  getters share one address (RVA 0x3E70 family), all `[rcx+0x10]` getters
  share 0xF6D0, `[rcx+0x20]` share 0x3F80, etc. Consequence: within a fold
  group any alias is functionally identical; across groups the offset IS the
  semantics. Old heuristic maps died on this (24 collision groups).
- FunPlus inserted ~6 custom exports inside the scrambled ordinal range —
  local +1 shifts that the alphabetical model must absorb (empirically:
  `assembly_get_image` reads `[rcx]` = stock `Il2CppAssembly.image@0`,
  mapping to `i00b3zvrxp71oai81eoaimktg`, NOT the alphabetically-zippered
  `i00atb5…` which is a custom).

## Verified map anchors (twin/thunk/disasm-proven)

| real | scrambled | proof |
| --- | --- | --- |
| il2cpp_init | i084v6jzbst | live init-detour fires |
| il2cpp_domain_get | i04d6j9ozbhlrslau | shares thunk target with mono_domain_get |
| il2cpp_domain_get_assemblies | i04ehl6vqz5d06bgl25m7ejwxbyr | walks assembly vector at .data+0x67e4be8 (same static mono_domain_get_assemblies_iter uses) |
| il2cpp_assembly_get_image | i00b3zvrxp71oai81eoaimktg | [rcx] body; live: 133 images enumerated |
| il2cpp_image_get_name | i083boux58fni0cbrjsm8 | [rcx] twin; live names correct |
| il2cpp_class_get_properties | i0274pa3h9jk80nvpoxcg78dyus | mono twin thunk target |
| il2cpp_class_value_size | i03e0cz23o9cta5pio4jied | mono twin thunk target |
| il2cpp_shutdown | i0ca4l1aqyxrkba | mono_runtime_quit twin |
| il2cpp_thread_attach | i0d7u24nehtxb6fhtm8d | mono_thread_attach twin |

Full 238-entry map: `assets/Il2CppNameMap.txt` (source of truth;
`../.interop-work/exportscan/` holds generation tooling + JSON).

## Internal function pinning

- **Class::Init = RVA 0x4F7E70**: first `E8 rel32` callee of
  `mono_class_instance_size` (RVA 0x49DD50), because mono_class_instance_size
  is exactly `Init(klass); return klass->instance_size;` (reads [klass+0x68]).
  Prologue (runtime bytes, REX-prefixed):
  `40 53 48 83 EC 20 48 8B D9 F6 41 61 01`
  (= push rbx; sub rsp,0x20; mov rbx,rcx; test byte[rcx+0x61],1 — the
  `initialized` bit test). Il2CppInterop's own signature scan
  (`E8 ? ? ? ? 0F B7 47 28 83`) never matches this build — hence the pinned
  fallback in the C# tree (Il2CppInteropClassInitFix).

## Runtime lessons (paid for with crashes — 2026-09-27)

- **`il2cpp_domain_get` (i04d6j9 → 0x52aab0) is CREATE-IF-MISSING**, not a
  pure getter: static NULL → allocate + run the domain initializer. Calling
  it from ANY thread before the game's own `il2cpp_init` completes
  constructs the domain mid-engine-init on the wrong thread →
  `Class::Init(NULL)` → c0000005. Readiness MUST be probed by reading the
  assemblies-array statics directly (decode them from
  `il2cpp_domain_get_assemblies`'s own code: first `48 8B 05` = end static,
  `48 2B 05` = begin static). Implemented in aniimod-ffi
  (`assemblies_vector_statics` + `domain_ready`).
- **get_corlib = i07bqcyg750u4sbax — LIVE-CONFIRMED** (Rust engine M1:
  returns an image whose `image_get_name` = "mscorlib.dll").
- `class_from_name(System.String)` → "String" round-trip works via
  i012anzg6nc6w94rw7jhsk (live-confirmed M1).
- Our version.dll forwarding must self-guard: wine can ignore
  LOAD_LIBRARY_SEARCH_SYSTEM32 and resolve "version.dll" to our own module
  → infinite recursion → stack-overflow crash (seen + fixed 2026-09-27).
- Only run the engine inside Aniimo.exe (UnityCrashHandler64.exe also
  loads version.dll from the game dir).
- The stale `C:\Program Files\dotnet` in the wine prefix (installed for
  ML 0.7.3 long ago) auto-hosts CoreCLR in game-adjacent processes and
  crashes with old signatures — sidelined to dotnet.aside-20260927.

## M7 state (2026-09-27 ~14:05)

BUILT + DEPLOYED (tamper-safe throughout):
- **CreateFileW IAT swap** (UnityPlayer): redirects LuaScripts.xdf opens →
  `AniiModLoader/Serviimo/LuaScripts.xdf` (Serviimo-generated via
  `tools/xdfconf.py`; existence-gated — pointing the game at a missing
  file aborts its Lua bootstrap with an int3 assert, learned the hard way).
- **getaddrinfo/GetAddrInfoW swap**: domain-suffix block list from
  `AniiModLoader/network.json` (vanilla live-service hosts).
- **anii-signal v0.3**: reads Mods/AniiSignal.json (OfficeBaseUrl,
  TelemetryPath, flush seconds — Serviimo writes this file), captures
  PosSyncComponent instances ON THE GAME THREAD at scene changes (3rd
  onward) — foreign-thread invokes of engine-allocating APIs crash this
  runtime (FindObjectsOfType from our thread = AV in Class::Init; Debug.Log
  likewise; property getters like Time are safe) — transform position via
  get_gameObject/get_transform/get_position invokes, batched telemetry
  POST (plain std-HTTP) to Serviimo office `/api/aniisignal/telemetry`.
- `tools/xdfconf.py`: XDF = plain ZIP; extract/build the redirected
  archive (tested: extracted FunPlus's real conf.json with their RSA keys).

OPEN ITEMS:
- Scene-capture probes were silent in automated boots — the rsaenh/
  FPXSteam Steam-auth flake has killed EVERY automated boot since ~11:25
  (30-50s in, before world entry). Manual user boots pass auth — the user
  playing into a world is the missing validation (watch for
  `[capture] scene scan` + telemetry POST lines).
- One boot DEADLOCKED (main thread CS-wait) when the console was spun up
  on a detached thread DURING il2cpp-init window; console now initializes
  on the engine thread pre-wait.

## ANTI-TAMPER: body patches DETECTED (2026-09-27 ~11:45, user's manual boot)

Body-patching GameAssembly (our PosSync trampoline) triggered the tamper
sweep: the game executed a deliberate **int3 (exception 0x80000003)** at the
patched body (crash RIP = the PosSync prologue; 0x80000003 = breakpoint) and
died ~8 s after load, at the login flow. This confirms the C# session's
"startup kills processes with patched GameAssembly code pages" wisdom — the
sweep can also fire later in the flow, not just at startup.

**Safe vs unsafe (hard rule going forward):**
- SAFE: slot swaps (methodPointer data writes), IAT swaps, GC-object data
  writes — 400 s soak + many boots, never tripped.
- UNSAFE: any byte modification of GameAssembly code pages (body patches,
  inline detours, Harmony-style native hooks).

**M6c (next):** hooking without code modification — hardware debug
registers (DR0-3) + vectored exception handler, or page-guard schemes.
Until then anii-signal uses slot hooks only (fires on reflection-path
calls only; WorldCapture needs M6c).

## M5/M6 session facts (2026-09-27 late)

- **M5 invoke+box PROVEN live**: runtime_invoke of Time getters returns a
  real box; klass == resolved System.Single; values read at offset +0x10.
  The earlier "0.000s" readings were REAL zeros — game_ready fires after the
  first scene change, before the player loop advances game time.
- **Debug.Log invoked from our (foreign) thread CRASHES this custom runtime**
  (deterministic, with or without hooks): its body lazily Class::Init's a
  class whose byval_arg[+0x50] is null → AV inside Class::Init's internal
  init (0x4f7030+0x41 reads [rax+0x28] on null). Avoid invoking methods
  whose bodies touch logger/stack-trace subsystems; simple property getters
  are safe. Lifecycle: use on_game_ready (post-first-scene), never invoke
  from on_load.
- **methodPointer slot swap only intercepts runtime_invoke/reflection** —
  the game's internal calls are direct-linked. Real hooking (WorldCapture
  class) needs body patches.
- **Body trampoline** (aniimod-hook::hook_body): relocate whitelisted
  prologue forms (rex push / sub rsp imm8+imm32 / reg-reg mov / lea with
  SIB-free disp8 / xor r,r) to an RX buffer + mov-rax/jmp-rax return, then
  patch mov rax,detour;jmp rax over the body. Refuses anything else (dumped
  hex). Debug::Log refused (rip-relative mov at byte 6 — needs disp fixups,
  skipped by design); **PosSync.SyncLogicWorldPosToServer hook ARMED**
  (prologue 4c8bdc 55 53 57 498d6ba1 4881ec…). Fire observation pending:
  Steam-auth (rsaenh/FPXSteam) wine flake killed 6 boots straight at
  ~30-50 s — pure game/crypto stack, pre-dates hooks, worsens with boot
  frequency (rate limiting suspected); scene events keep firing every boot.
- Box layout confirmed: klass@0, value@0x10 (Single verified by pointer
  equality with the resolved class).

## Milestone status (2026-09-27, end of M2/M3/M4 session)

- [x] M0 — Rust version.dll loads in vanilla game; 133 images enumerated;
      Class::Init pinned live. VERDICT: PASS.
- [x] M1 — get_corlib/class_from_name/class_get_name/methods round-trip.
      VERDICT: PASS. 400 s soak: game alive throughout, reached FPX login.
- [x] M2 — event taps LIVE: GetProcAddress IAT swap on **UnityPlayer.dll**
      (NOT the exe — the engine resolves il2cpp symbols through
      UnityPlayer's own KERNEL32 import [descriptor 0], resolved before our
      version.dll DllMain runs, so patching from DllMain is race-free);
      init + runtime_invoke detours armed; 4 Internal_ActiveSceneChanged
      events observed in one boot.
- [x] M3 — mod SDK live: host loads Mods/*.dll (LoadLibraryW +
      aniimod_entry + catch_unwind), mods get find_class + on_scene_changed
      + log. 2 mods loaded per boot.
- [x] M4 (starter) — anii-signal.rs online; probed
      FunPlus.WorldX.Entities.Components.PosSyncComponent + SceneManager OK.
- [ ] M5 — method calls, field reads, anii-signal telemetry features.

Map correction this session: **runtime_invoke = i0bbaapo4q33rkcxy16q8**
(body-verified prologue 48 89 5C 24 08 …; the alphabetical model had it
mirrored with convert_args = i0ba9un4… — body evidence beats model
inference). Known environmental flake, NOT loader: rsaenh/fpxsteam AV
(Steam auth crypto under wine) kills some boots at ~40 s — stack is purely
rsaenh/advapi32/fpxsteam; other boots sail past it.

## Struct offsets observed (stock-consistent)

- Il2CppImage: name@0x0, nameNoExt@0x8, assembly@0x10, typeStart@0x18
- Il2CppAssembly: image@0x0
- Il2CppClass: byval_arg@0x50, name@(+0x50→0x30), instance_size@0x68,
  initialized-bit@0x61, num_fields@(+0x50→0xA2, word), rank@0x64 (byte)
- MethodInfo: name@0x18, class@0x20, return_type@0x28, token@0x48,
  flags@0x4E(word), paramCount@0x52(byte), is_generic bit@0x53.0
- FieldInfo: name@0x0, type@0x8, parent@0x10, offset@0x18

## Wine/Proton environment (proton-cachyos-11.0, prefix compatdata/4126040)

- Loader entry = native `version.dll` beside Aniimo.exe; requires
  `[Software\Wine\AppDefaults\Aniimo.exe\DllOverrides] "version"="native,builtin"`
  in the prefix user.reg (Steam's LaunchOptions were lost once already —
  never rely on them) plus `steam_appid.txt` (4126040) for direct launches.
- Engine requests symbols via GetProcAddress; **redirect only
  Aniimo.exe's IAT** (plthook on the main module) — pointer-level, no
  GameAssembly code pages touched.
- rsaenh/fpxsteam crash (~40 s) only on Steam-launched boots without the
  loader env; direct-Proton launches (harness default) avoid it.
- GMESDK.dll present (voice); the "internal error in .NET Runtime" crash
  signature was coreclr AV, not GME.

## Interop artifacts already generated (reusable)

- `AniiModLoader/Il2CppAssemblies/` — 133 interop assemblies
- `AniiModLoader/Il2CppNameMap.txt` — deployed copy of the verified map
- decrypted metadata + DummyDlls: `../.aniisignal-parked/aniimo-loader-kit/`

## Tooling

- `../tools/exportmap/` — exportscan/twinmatch/thunkmatch/pinmap/alignmap/
  windowresolve/finalmap + mdwalk (minidump walker). Regenerate the map on
  game updates, then re-verify anchors.
- `../tools/aniiharness/aniiharness.py` — deploy/launch/watch/kill/run CLI.
- C# tree `../AniiModLoader_C` — parked at commit-candidate state with
  Core.cs managed-fix restructure + support-module guards + ClassInitFix.

## V3 game update (2026-09-28) — the update-response session

### What the update changed (depot manifest diff 7678163621633787445 → 8272606227834818089)

- `GameAssembly.dll`: content changed in BOTH the 09-27 and 09-28 builds —
  every scrambled export name rotated (zero overlap with the V2 set, 245
  scrambled slots both builds, ordinals 24..268 contiguous) — but the code
  layout is BYTE-IDENTICAL: same RVA + same ordinal for every anchor, and
  all 238 mapped entries matched byte-for-byte at the same ordinal.
  Conclusion: the update only re-ran the export scrambler.
- `global-metadata.dat`: BYTE-IDENTICAL across 09-24/09-27/09-28 → the
  static decrypt key, dump, DummyDlls, class/method offsets ALL stay
  valid; a re-dump is unnecessary on such updates.
- `NEP2.dll`: `.200` VM payload fully re-encrypted (~87% of bytes differ,
  no contiguous structure; entropy 7.8). `.text` differs by only 282 bytes
  in 138 clusters — ALL of them `jmp <into .200>` thunk retargets (the VM
  blob moved, the thunks follow). Imports identical. NO semantic native
  change; the anti-cheat update is rules/payload-only inside the VM.
- `NEPKernel.sys` (game-dir drop, not in depot): UPX-packed, NetEase
  (Hangzhou)-signed kernel driver exposing `\Device\NEPKernel` /
  `\??\NEPKernel`. Never loads under Proton (wine does not load Windows
  drivers) — NEP2 runs user-mode-degraded and the game tolerates it.
- `LuaScripts.xdf` (+33 KB) / `.xdt`, one video, md5list: data refresh.
  Yidun Lua gate unchanged: `ENABLE_YI_DUN_SDK` gray flag gates the whole
  SDK; creds ship in plaintext in `SDK/YiDunUtils.lua` (gameKey
  `7s9k2p5z8q4x6r1t3g7b9n2m5d1f8h3j`, businessIdWindows
  `22dec3feaa4cfe2af5efe70faac4fe1a`, productId `YD00650763029250`).

### Update runbook (M9, now proven end-to-end)

1. `python3 tools/manifest_diff.py` old new (or diff shas of the 5 key
   files) → did GameAssembly content change?
2. If metadata sha unchanged → no re-dump; run `exportscan.py` on the new
   binary, then `regen.py --scan ... --old-scan ... --old-map ... --out
   assets/Il2CppNameMap.txt` — the fast ordinal join ABORTS if any body
   byte differs at the same ordinal (fall back to twin alignment only if
   a future update recompiles).
3. Update `EXPECTED_GAME_ASSEMBLY_SHA512_PREFIX` in aniimod-map (regen.py
   prints it), update the anchor test values, rebuild, deploy.
4. The loader refuses to run on an unknown build (SHA gate, wired
   2026-09-28): logs FATAL + writes `AniiModLoader/UPDATE_NEEDED.txt`.

### v0.4.x engine changes

- SHA-512 gate at bind time (streamed, ~200 ms for 115 MB).
- `events.rs` scrambled names resolve from the map crate (no more
  hardcoded name constants to rot on updates).
- Env snapshot in the boot log (`wine_get_version`, WINEPREFIX, Steam IDs).
- frida-gadget hosting (opt-in): `AniiModLoader/frida-gadget.dll` +
  `frida.enabled` marker → listen 127.0.0.1:27042; attach with
  `tools/frida/frida_run.py` (gadget has no process enumeration — attach
  by name 'Gadget'). Used for the NEP2 boot trace (see below).
- Deterministic redirect arming: CreateFileW / getaddrinfo /
  GetAddrInfoW are imported BY ORDINAL by every module (name-based IAT
  walks see nothing) — `swap_module_import_by_address` sweeps the RESOLVED
  addresses across 11 modules from engine_main. The old GetProcAddress-tap
  arming was boot-dependent; this one fires every boot.

### NEP2 dynamic profile under wine (frida, 55 s boot window)

- Opens `\\.\{00000001-0000-0000-0000-4E6574446576}` and -0002- ("NetDev"
  GUID device) 3x → IOCTLs follow — driver channel; fails under wine,
  game continues (degraded mode confirmed).
- Writes/reads `C:\ProgramData\NeteaseWinDev\isc.dat`.
- Full PhysicalDrive0-15 + `\\.\Scsi0:"`..Scsi15 sweep (disk/SCSI
  fingerprinting; note the trailing-quote in the Scsi names — their bug).
- Re-reads its own NEP2.dll 3x + kernel32.dll (self-integrity hashing).
- One IsDebuggerPresent; 174 GetProcAddress dynamic resolutions.
- NO VirtualProtect/VirtualQuery on GameAssembly in the boot window —
  the code-page sweep runs later (minutes in), consistent with the
  2026-09-27 int3 observation during gameplay.

### Login-video "Illegal characters in path" (pre-existing, wine)

`Path.GetFileName(type)` throws inside `RawFileCreateType2Path` ←
`RawFileResIDToRelativePath` ← `PVideoController.OpenVideo` at the login
screen (+24-26 s) — present in 09-27 crash logs too (NOT a V3 regression).
Non-fatal (caught+logged); the login background video just never plays.
PosSyncComponent has NO position field (flags only: UpdateVelocity@0x30,
SyncToServer@0x31) — the transform chain in anii-signal is the correct
capture path; entity identity = BaseComponent `<owner>k__BackingField`
@0x18 → Entity `<id>k__BackingField` (string) @0x1B8 (anii-signal v0.3.1
resolves both name-driven, live-verified).

### Serviimo identity switch (the badSize lesson)

- `LuaScripts.xdt` = JSON manifest {CMDataLen = EXACT XDF byte size,
  CMDataMD5, CMEntryNum, CMList[{CEName, CEMD5}/entry]}. A substituted
  XDF without a matching XDT dies at load:
  **"LuaData Corrupted: badSize, plz repair!"** (live 2026-09-28).
  `tools/xdfconf.py` now emits BOTH files (CMList md5 patched for the
  conf.json entry); net.rs redirects `lua\luascripts.xdt` as well.
  LIVE-VERIFIED: game accepted the pair and reached the login screen.
- `fpx_config_pc` = b64(DES-ECB(PKCS5(json))), key `FPX_FPX_` (round-trip
  verified). `tools/fpxconf.py` rewrites every URL to the Serviimo office
  and regenerates `fpx_md5sums`; net.rs redirects both. BUT the FPX SDK
  often bypasses it: FPXStoreUrlManager probes a CACHED store URL with its
  own HTTP stack (CEF-like; ignores ws2_32 IAT detours) — hence the
  hosts-file kill switch in the wine prefix pinning every vanilla host to
  127.0.0.1 (`.../drivers/etc/hosts`, backup `hosts.anii-backup`).
- The rsaenh crash is pinned: `rsaenh→advapi32→fpxsteam` AV read@0x60 in
  FPXSteam's post-login crypto — wine's crypto CSP bug on the FunPlus
  Steam-auth path. Serviimo-side auth removes this path entirely.
