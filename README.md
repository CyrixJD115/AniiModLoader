# AniiModLoader (Rust engine)

> ⚠️ **Heavily work in progress.** Some functions work, some don't. Any
> contribution really helps — here or in the sibling ecosystem modules
> ([Serviimo](https://github.com/CyrixJD115/Serviimo),
> [Apiimo](https://github.com/CyrixJD115/Apiimo)) — all coordinated from
> [AniiStudio](https://github.com/CyrixJD115/AniiStudio).

A native mod-loader engine for Aniimo (Steam app 4126040, Unity 2022.3.62f1
il2cpp, Proton/Wine), written entirely in Rust.

## Docs
- [Legal notice](LEGAL.md) — reverse-engineering statement, bring-your-own-game rule, acceptable use, DMCA policy
- [Notice](NOTICE.md) — short-form disclaimer and attribution
- [License](LICENSE) — GPL-3.0

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

The full legal terms — reverse-engineering statement, bring-your-own-game
rule, AI-assisted-development disclosure, and DMCA policy — live in
[LEGAL.md](LEGAL.md).

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

## Legal

<details>
<summary><strong>⚖️ Full legal notice — click to expand</strong> (canonical file: <a href="LEGAL.md">LEGAL.md</a>)</summary>

**Effective date: 2026-10-15.** The current version of this notice is the one
on the default branch.

This document is a good-faith legal notice for the AniiModLoader project. It
is **not legal advice**, creates no attorney–client relationship, and
guarantees no particular legal outcome in any jurisdiction.

### 1. Nature of this project

AniiModLoader is a personal, non-commercial, fan-made research project. It is
**not affiliated with, endorsed by, or sponsored by** the developers or
publishers of Aniimo or any of their partners. "Aniimo" and all related names,
marks, and assets are the property of their respective owners; they are used
here only to identify the game this project interoperates with (nominative
use).

### 2. No copyrighted game content is distributed

This repository distributes only **original code and documentation written
for this project**. It does not contain, host, or link to any part of the
game or its official services — no binaries, no assets, no audio or video, no
extracted data files, and no server content. Cloning this repository provides
nothing from the game.

Technical artifacts that do appear here (memory addresses, file offsets,
data-layout descriptions, export notes) are factual observations about how
the software behaves. They are uncopyrightable functional facts, are not
creative expression, and are not substitutes for any part of the game.

### 3. Bring your own game

Every tool in this project expects you to supply your own game files,
obtained through **your own legitimate Steam account**. This project does not
distribute game files, does not automate their acquisition, and will not
assist anyone in obtaining them unlawfully.

### 4. Reverse-engineering statement

The reverse-engineering and analysis behind this project was performed on
copies of the software legitimately owned by the author, for the purposes of:

- **interoperability** — making independently created software (this loader
  and the user's own Serviimo server) work with the author's own copy of the
  game, the purpose expressly contemplated by 17 U.S.C. § 1201(f) in the
  United States and by Arts. 5–6 of EU Directive 2009/24/EC in the European
  Union; and
- **personal study, education, and research** (see 17 U.S.C. § 107).

This project is not a piracy tool: it does not defeat copy protection in
order to access, copy, or redistribute copyrighted works, and where analysis
touches technical measures at all, it is solely to achieve interoperability
with the user's own copy. Reverse-engineering rights vary by jurisdiction —
**users are responsible for ensuring their use is lawful where they live.**
If your jurisdiction does not permit this kind of analysis, do not use these
tools.

### 5. Acceptable use — mods are for Serviimo, not for cheating

Mods in this ecosystem exist for exactly one purpose: **playing on your own
private [Serviimo](https://github.com/CyrixJD115/Serviimo) instance**, the
self-hosted server reimplementation, with your own copy of the game and your
own account.

The following are **strictly prohibited** and unsupported:

- cheating or gaining any unfair advantage in the game or any online service;
- using AniiModLoader, its mods, or derivatives to tamper with, degrade, or
  attack the official Aniimo servers or any other online service;
- evading or defeating anti-cheat systems. Anti-cheat evasion is not a
  feature, a goal, or an accepted contribution of this project;
- commercial use of any kind.

### 6. AI-assisted development

Much of the analysis and code in this project was developed with AI
assistance, operating exclusively on the author's own legitimately obtained
copies of the software. The resulting repository content is original code and
factual technical documentation, not reproductions of third-party material.

### 7. Takedowns and DMCA policy

If you are a rights holder and believe material in this repository infringes
your rights, please open a GitHub issue identifying the material and the
basis for the claim, or use GitHub's DMCA takedown process. Legitimate
reports are reviewed promptly, and genuinely infringing material will be
removed. Because this repository intentionally contains no third-party
copyrighted content, blanket or mistaken takedowns may be answered with a
counter-notice where appropriate.

### 8. No warranty; limitation of liability

This software is provided **"as is", without warranty of any kind**, express
or implied. To the maximum extent permitted by law, the authors are not
liable for any damages arising from its use. Where the GPL-3.0 license
applies, its sections 15–16 govern warranty and liability; this notice
supplements, and does not replace, that license.

### 9. Your responsibility

By using this project you acknowledge that you are responsible for complying
with the game's terms of service, the Steam Subscriber Agreement, and all
laws that apply to you.

</details>
