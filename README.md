# AniiModLoader (Rust engine)

> ⚠️ **Heavily work in progress.** Some functions work, some don't. Any
> contribution really helps — here or in the sibling ecosystem modules
> ([Serviimo](https://github.com/CyrixJD115/Serviimo),
> [Apiimo](https://github.com/CyrixJD115/Apiimo)) — all coordinated from
> [AniiStudio](https://github.com/CyrixJD115/AniiStudio).

A native mod-loader engine for Aniimo (Steam app 4126040, Unity 2022.3.62f1
il2cpp, Proton/Wine), written entirely in Rust.

## Purpose and acceptable use — read this first

Mods in this ecosystem exist for exactly one purpose: **playing on your own
[Serviimo](https://github.com/CyrixJD115/Serviimo) instance** — the private,
self-hosted Aniimo server reimplementation. Any other use is unsupported and
strictly prohibited:

- **Cheating is banned, full stop.** Using AniiModLoader, its mods, or
  anything built on them to cheat, gain an unfair advantage, harass players,
  or otherwise tamper with the official Aniimo servers or any other online
  service is prohibited.
- **Anti-cheat evasion is not a feature, a goal, or a supported use.** Do not
  ask for it and do not build it on top of this project.
- The loader targets your own local copy of the game together with your own
  local Serviimo server. That is the entire scope.

The predecessor C# engine (MelonLoader fork, "AniiModLoader v0.7.3") is parked
at `../AniiModLoader_C`. It reached: loader boots, export map verified live
(133 images enumerated), interop assemblies generated, `AniiSignal` mod loads.
Everything after that point crashed inside **CoreCLR running under Wine**
(native-to-managed trampoline marshaling) — the entire failure class that this
Rust engine deletes by not shipping a .NET runtime at all.

## Entry mode

`aniimod-host` builds to `version.dll` (proxy DLL). The game loads it at
process start through Wine's `DllOverrides` (already configured in the Proton
prefix — see `docs/findings.md`). No external injector process, no remote
threads, no debugger attach: from the OS point of view the game simply loads
a DLL it asked for.

## Workspace layout

| crate | responsibility |
| --- | --- |
| `aniimod-map` | the verified scrambled-export map (238 entries) + loader/validator; regenerating tooling stays in `../tools/exportmap` (python) |
| `aniimod-ffi` | il2cpp C API through the map: `resolve("il2cpp_domain_get")` → real function pointer via `GetProcAddress` on the scrambled name; typed wrappers |
| `aniimod-metadata` | decrypt + parse `global-metadata.dat` (lifted from apiimo-core) so the engine can resolve any (image, class, method) by name without Cpp2IL or prebaked assemblies |
| `aniimod-hook` | pointer-level hooking: GetProcAddress redirect on the engine's imports (proven, zero code-page patches) and optional trampolines where truly needed |
| `aniimod-host` | the `version.dll` cdylib itself: bootstrap, il2cpp init tap, mod discovery/loading, event taps, log |
| `aniimod-mod` | mod SDK: the `Mod` trait + `#[aniimod::main]` macro |
| `mods/anii-example` | example mod skeleton |

## Build

```sh
rustup target add x86_64-pc-windows-gnu   # once; mingw (x86_64-w64-mingw32-gcc) is already installed
cargo build --release -p aniimod-host --target x86_64-pc-windows-gnu
# → version.dll → copy next to Aniimo.exe
```

Test/iterate loop is owned by the existing harness:

```sh
python3 ../tools/aniiharness/aniiharness.py --verbose run --max-iterations 3
```

## Plan of record

**[PLAN.md](PLAN.md)** — why Rust, ground rules, milestone roadmap
(M5 method calls → M6 hooking → M7 full anii-signal → M9 update
resilience), acceptance criteria, and the verification loop.

## Status

- **M0–M4(starter) COMPLETE (2026-09-27)** — boots in the vanilla game,
  enumerates all 133 images, resolves classes by name, taps
  il2cpp_init/runtime_invoke through UnityPlayer's IAT, fires scene events,
  and loads Rust mods from Mods/ (anii-example + anii-signal both online
  per boot). See `docs/findings.md` for live-verified facts and lessons.
- Next: M5 — method calls, field reads, full anii-signal telemetry.

Test loop: `python3 ../tools/aniiharness/aniiharness.py --verbose run
--max-iterations 3` — deploy is just copying the built `aniimod_host.dll`
to the game dir as `version.dll` (+ `steam_appid.txt` for direct launches;
the prefix DllOverride already persists).

## Legal notice

- **Not affiliated.** This is a personal, fan-made project. It is not
  affiliated with, endorsed by, or sponsored by the developers or publishers
  of Aniimo. All game names, trademarks, and assets belong to their
  respective owners.
- **No game content is distributed.** This repository contains no part of the
  game — no binaries, assets, code, or data. Cloning it gives you nothing
  from the game.
- **Bring your own game.** You must provide your own game files, obtained
  through your own legitimate Steam account. This project never distributes
  game files or helps anyone acquire them unlawfully.
- **Interoperability and research.** The loader exists for interoperability
  with your own legitimately obtained copy of the game (through your own
  Serviimo server), and for personal education and reverse-engineering
  research on that copy.
- **Rights holders.** If you believe something in this repository infringes
  your rights, please open an issue describing it; legitimate reports will
  be reviewed and resolved promptly.
- **No warranty.** Everything is provided "as is", without warranty of any
  kind. You are responsible for how you use this software and for complying
  with the game's terms of service and the laws that apply to you.
