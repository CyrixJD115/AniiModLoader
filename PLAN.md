# AniiModLoader Engine — Plan of Record

**Repo:** `AniiModLoader_src` (Rust). The parked C# MelonLoader fork lives in
`../AniiModLoader_C` as fallback/reference; its research is baked into this
engine and `docs/findings.md`.

**One job: be the mod loader engine for Aniimo** (Steam 4126040, Unity
2022.3.62f1 il2cpp, obfuscated exports, played under Proton/Wine).

---

## Why Rust (decision, 2026-09-27)

The C# fork reached "1 Mod loaded" and then died inside **CoreCLR running
under Wine** during native↔managed trampoline setup — an unpatchable failure
class (a managed runtime inside a compatibility layer). The Rust engine has
no runtime to break: one ~350 KB `version.dll`, direct C FFI to the il2cpp
API through the verified export map, startup finished in <1 s, 400 s soak
clean while the game reached login. Cost of the pivot: no C# mod ecosystem,
no Harmony, no Il2CppInterop conveniences — everything below rebuilds those
pieces natively, sized to what our mods actually need.

## Ground rules (inherited, non-negotiable)

1. Never write to game files. Everything lives under `AniiModLoader/` and `Mods/`.
2. Hook by pointer swap (IAT) by default; inline code-page detours are
   opt-in (`--aniimod.trampolines`) and must be justified per milestone.
3. Never call any il2cpp function before the game's own `il2cpp_init`
   completes — readiness is probed by reading statics
   (`Il2Cpp::assemblies_vector_statics`), because `il2cpp_domain_get` is
   create-if-missing and will crash the game from a background thread.
4. Body evidence beats model inference for the export map. Regenerate +
   re-verify on every game update (`tools/exportmap`, checksum gate).
5. Every change ships through the harness loop: build → deploy → boot →
   verdict (`../tools/aniiharness/aniiharness.py`).

## Deployed state today (2026-09-27)

- `version.dll` = engine v0.2.1 (console window included)
- `Mods/anii_example.dll`, `Mods/anii_signal.dll`
- Launch options: `WINEDLLOVERRIDES="version=n,b" %command%`
- Per boot, proven live: 133 images enumerated, class resolution,
  init/invoke taps via UnityPlayer IAT, scene events, 2 mods online.

---

## Milestones

### DONE — M0: engine core
version.dll loads in the vanilla game; map-driven FFI; safe readiness probe;
133-image enumeration. *Acceptance:* harness PASS + log lines. ✅ 2026-09-27

### DONE — M1: API depth
get_corlib → "mscorlib.dll"; class_from_name round-trip (System.String). ✅

### DONE — M2: event taps
UnityPlayer.dll GetProcAddress IAT swap; init + runtime_invoke detours;
scene-change events fire (4 observed in one boot). ✅

### DONE — M3: mod SDK
`Mods/*.dll` load via `aniimod_entry`; mods get `log` / `find_class` /
`on_scene_changed`; per-mod panic isolation (catch_unwind). ✅

### DONE — M4 (starter): anii-signal-rs skeleton
Loads every boot; probes `PosSyncComponent` + `SceneManager` successfully. ✅

---

### DONE — M5: call into the game (2026-09-27)
find_method / find_field / make_string / call_method (via the tap-captured
original runtime_invoke) / read_static_field / unbox_f32 / string_length /
peek_u64. *Acceptance:* Time getters invoked from on_game_ready return real
Single boxes (klass pointer-equal to resolved System.Single, plausible
values); String::Empty static read = len 0. ✅ LESSON: Debug.Log from a
foreign thread deterministically crashes the custom runtime — use the
on_game_ready lifecycle, avoid logger-touching invokes.

### M6: method hooking — SPLIT by anti-tamper finding (2026-09-27 ~11:45)
The user's manual boot proved body patches on GameAssembly are DETECTED:
deliberate int3 (0x80000003) at the patched PosSync body, game dead ~8 s
after load. Hard rule: **no GameAssembly code-page writes, ever.**
- DONE: slot swap (`hook_method`) — data-only, tamper-safe, intercepts
  runtime_invoke/reflection paths.
- BUILT BUT UNUSABLE: body trampoline (`hook_method_body`) — works
  mechanically, triggers the sweep. Kept in the codebase behind the
  whitelist gate for reference; do not deploy.
- DONE — M6c-alternative: **POLL CAPTURE (2026-09-27)** — the tamper-safe
  capture design. Instead of intercepting the sync method, a host-owned
  attached thread calls `Object::FindObjectsOfType(PosSyncComponent)` every
  2 s (a plain, allowed invoke) and hands each live instance to the mod
  callback (instance-field reads via `il2cpp_field_get_value`). Zero code
  modification — invisible to the sweep. SDK: `poll_instances` +
  `array_length`/`array_item`/`read_field_*`/`find_objects_of_type`.
  *User validation:* play into a world → `[capture]`/`[poll]` lines in
  AniiModRust.log.
- M6c proper (hardware breakpoints / VEH) stays on the shelf — only needed
  if polling can't see something we genuinely need (e.g. exact sync-call
  arguments rather than component state).

### M7 — AniiSignal = Serviimo-only client (MISSION per user, 2026-09-27)

Directive: AniiSignal talks to **Serviimo ONLY**. The user controls the
server instance (own files, own saves). Client must show **their server
listings from config** and **completely block ALL vanilla live-service
servers**.

Design — every mechanism tamper-safe (no GameAssembly code writes):

1. **Identity/config: LuaScripts.xdf file redirect.** The game reads its
   entire live-service setup (gate keys, gate addresses, server list,
   `enableOnlineServerList`, `forceMsGate`) from `Network/conf.json`
   inside `LuaScripts.xdf`. Mechanism: IAT-swap `CreateFileW` on
   UnityPlayer.dll (same proven safe swap as GetProcAddress) and redirect
   any open of that one file to `AniiModLoader/Serviimo/LuaScripts.xdf` —
   a Serviimo-generated copy with the substituted conf.json (Serviimo
   keys → client encrypts to OUR gate; embedded serverGroupData = the
   user's `Servers[]` clusters from config; online list disabled).
   Mirrors the C# GateConfigInjector but at file level — lower, safer.
   *Serviimo side:* needs an XDF repack command (C# sessions know the
   container format; conf.json already extracted once).

2. **Vanilla block (belt & suspenders): connection filter.** IAT-swap
   `connect`/`WSAConnect` on UnityPlayer.dll: any outbound destination
   that is not in the allowlist (Serviimo gate/list, Steam, CDN for game
   assets) gets refused client-side. Vanilla live-service servers become
   unreachable BY CONFIG first (step 1) and unreachable BY SOCKET second.

3. **Telemetry: poll capture → Serviimo.** M6c poll stream (positions,
   entity ids) batched and POSTed to Serviimo's office HTTP
   (`/api/aniisignal/telemetry` → SQLite `world_telemetry` — the C#
   round-trip already proved the schema). Local HTTP = plain std
   TcpStream, no TLS dependency needed.

4. **Config: `Mods/AniiSignal.json`** (Serviimo writes it — schema already
   exists from the C# mod: GateAddress, Servers[], menu/pin modes,
   TelemetryFlushSeconds; plus new BlockVanilla=true).

*Acceptance (same live bar as the C# mod):* game on, Serviimo on, client
server list shows ONLY the user's clusters, vanilla endpoints refused in
the log, positions land in `world_telemetry`, saves live on Serviimo.

### M8 — QoL hardening
- `AniiModLoader/aniimod.toml` (console on/off, log level, mod order).
- Mod manifest + friendly load errors; disable-on-crash backoff.
- Console: input line (commands: `mods`, `reload <mod>`, `quit`).
- *Acceptance:* config respected across a boot; `reload` re-ons a mod.

### M9 — game-update resilience — DONE 2026-09-28 (V3 update, live-proven)
- SHA-512 gate wired at bind time: streamed hash (~200 ms), FATAL log +
  `AniiModLoader/UPDATE_NEEDED.txt` on mismatch, engine stands down.
- `tools/exportmap/regen.py`: one-shot regen — fast path is the PROVEN
  ORDINAL JOIN (V3 update rescrambled every export name but left code
  layout byte-identical; all 245 slots verified byte-equal, runtime_invoke
  prologue re-verified at its thunk target). Aborts loudly if any body
  byte differs at the same ordinal; twin alignment is the documented
  fallback for a future recompile.
- `events.rs` resolves its scrambled names from the map crate — a
  rescramble now needs a new map asset + rebuild, never code edits.
- Full runbook + what the V3 update changed: `docs/findings.md`
  "V3 game update (2026-09-28)".

### M8.5 — Serviimo-only identity switch (2026-09-28, in flight)
User direction: the client never talks to FunPlus live services — only to
the user's Serviimo instance (own files, own saves, own account).
- DONE: XDF+XDT substitution pair (badSize fixed, live-verified to login
  screen); fpx_config_pc/fpx_md5sums redirect + `tools/fpxconf.py`;
  deterministic ordinal-import IAT sweep for CreateFileW/getaddrinfo;
  wine-prefix hosts pin of every vanilla host (FPX SDK's own HTTP stack
  ignores IAT detours); office request-capture logging on the FPX account
  paths.
- OPEN (next milestone): Serviimo account module — decode/encode the
  FPX envelope (b64(AES-128-CBC)+HMAC, keys from the cracked config),
  implement email sign-up/login + gate ticket mint; office listeners on
  :80/:443 for capture; the rsaenh/FPXSteam post-login crash (wine CSP
  bug) becomes unreachable once auth flows through Serviimo.

### M10 — (optional, decide later) C# interop bridge
If a future mod genuinely needs C#, evaluate hosting a minimal CLR
*sidecar process* (IPC to the engine) rather than in-process CoreCLR.
Default answer stays no — this exists only so the question is on record.

---

## Verification loop (every milestone)

```sh
cd AniiModLoader_src
cargo build --release -p aniimod-host --target x86_64-pc-windows-gnu
cp target/x86_64-pc-windows-gnu/release/aniimod_host.dll <game>/version.dll
cp target/x86_64-pc-windows-gnu/release/<mod>.dll <game>/Mods/
python3 ../tools/aniiharness/aniiharness.py --verbose run --max-iterations 3
```

Green = `VERDICT: PASS` + the milestone's marker lines in
`AniiModLoader/AniiModRust.log`. Known environmental flake to ignore:
rsaenh/fpxsteam AV ~40 s on some boots (Steam-auth crypto under Wine; pure
game/wine stack, other boots pass it).

## Documentation map

- `docs/ARCHITECTURE.md` — design, boot sequence, hook strategy.
- `docs/findings.md` — live-verified RE facts, offsets, crash lessons.
- `../.interop-work/exportscan/` — map artifacts + regeneration tooling.
- Memory: `rust-modloader-pivot` (session state), `aniisignal-melonloader-debug`
  (full C# history incl. Serviimo test rig).
