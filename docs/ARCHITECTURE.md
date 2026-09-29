# AniiModLoader Rust engine — architecture

Answers the design question: *what if the loader is injected at runtime
(inside Aniimo.exe / GameAssembly.dll) and the whole engine is Rust?*

## Why native Rust is the right call here

Everything the C# stack proved WORKS is native-side knowledge; everything it
still FAILS on is managed-runtime-under-Wine:

- proven: proxy-DLL loading under Proton, pointer-level `GetProcAddress`
  redirect of `il2cpp_init`, scrambled-export resolution via the verified
  map, full domain enumeration (133 images), metadata decryption, interop
  assembly generation, mod assembly loading.
- failing: CoreCLR 6 inside wine crashes in native↔managed trampoline
  marshaling (scene/delegate events); hostfxr/CoreCLR bring GMESDK + rsaenh
  flakiness with it; every wine quirk multiplies through two VMs.

A Rust engine is one native library in the game process. No VM, no JIT, no
marshaling layer, no runtime deployment (`Dependencies/dotnet` disappears).
The il2cpp API is a flat C ABI — direct FFI, resolved through the export map,
which removes the entire PinvokeRemap/ldstr-remap machinery.

## Process layout

```
Aniimo.exe (wine/Proton)
 └─ version.dll                        ← aniimod-host (Rust cdylib)
     ├─ forwards genuine version.dll APIs (delay-resolved, rarely used)
     ├─ aniimod-hook: PLT/IAT redirect of GetProcAddress on Aniimo.exe
     │    engine asks for scrambled i084v6jzbst (= il2cpp_init)
     │    → our detour instead (pointer swap, no code pages touched)
     ├─ on il2cpp_init(original first):
     │    1. aniimod-map   : load + sanity-check the export map
     │    2. aniimod-ffi   : bind the il2cpp API (typed fns)
     │    3. aniimod-metadata: decrypt + index global-metadata.dat
     │    4. enumerate images/classes lazily as mods ask
     │    5. aniimod-host  : load Mods/*.dll (Rust cdylibs), call Mod::on_load
     │    6. event taps (below)
     └─ log → AniiModLoader/AniiModRust.log (+ trace)
```

## The four hard problems and their Rust answers

### 1. Scrambled exports
`aniimod-map` embeds `assets/Il2CppNameMap.txt` (238 verified entries,
`real=scrambled`). Verification tooling (mono-twin pinning, ordinal
alignment) lives in `../tools/exportmap` and regenerates the map on game
updates. `aniimod-ffi` resolves every API through it:
`GetProcAddress(GameAssembly, map["il2cpp_domain_get"])`.
The map is checksum-anchored to GameAssembly's SHA-512 (like the C#
generator cache) so a game update fails loudly instead of calling garbage.

### 2. Metadata / method resolution without Cpp2IL
`aniimod-metadata` lifts apiimo-core's decrypt + parser. It yields
images → typedefs → fields/methods by name and index, so a mod can do
`engine.find_method("UnityEngine.CoreModule", "Application",
"sceneLoaded")` and get a `*const MethodInfo`. No dummy DLLs, no interop
generation, no assembly loading. (AniiSignal's needs — class field reads,
method calls, delegate subscription — are all reachable this way.)

### 3. Events without managed trampolines
Layered, most-preferred first:
1. **Pointer redirect of `il2cpp_runtime_invoke`** (same GetProcAddress swap
   as init; the engine calls all managed entry through it) — observe invokes
   to `Application.sceneLoaded`-family add_* methods and subscribe by
   invoking the same method with our own native callback via
   `il2cpp_delegate` creation on the il2cpp side (no CLR involved).
2. **Trampoline detours** (`aniimod-hook`, retour/minhook-style) where a
   pointer swap can't reach — acceptable: today's boots show interop's own
   code-page detours on GameAssembly survived this build's startup.
3. **Polling fallback** for simple state (title-screen reached, etc.).

### 4. Wine/Proton environment
- DLL override already fixed persistently in the prefix registry
  (`[Software\Wine\AppDefaults\Aniimo.exe\DllOverrides] version=native,builtin`).
- Launch orchestration stays in `aniiharness` (direct Proton launch with
  `WINEDLLOVERRIDES`, log watch, kill, verdict) — engine just logs.

## Mod SDK sketch

```rust,ignore
// mods/anii-example/src/lib.rs
use aniimod_mod::{aniimod, Mod, Engine, Event};

pub struct Example;

#[aniimod(name = "anii-example", version = "0.1.0", author = "you")]
impl Mod for Example {
    fn on_load(&mut self, engine: &mut dyn Engine) -> anyhow::Result<()> {
        let scene_loaded = engine.find_method(
            "UnityEngine.CoreModule", "UnityEngine.SceneManagement.SceneManager",
            "add_sceneLoaded")?;
        engine.subscribe(scene_loaded, |args| {
            // native callback, no VM
        });
        Ok(())
    }
}
```

Mods are `cdylib`s exporting `aniimod_entry() -> Box<dyn Mod>`; the host
dlopen()s them from `Mods/`.

## Milestones

- [ ] M0 map+ffi: enumerate + print all 133 images from inside the process
- [ ] M1 metadata: resolve + call a void() method (e.g. a logger icall)
- [ ] M2 host: version.dll proxy loads, boots game, logs, exits clean
- [ ] M3 events: scene-load tap fires a native callback
- [ ] M4 mod-sdk + example mod loaded from Mods/
- [ ] M5 anii-signal port (server-list + telemetry listener)

## Guardrails carried over

- never write to game files; everything lives under `AniiModLoader/`
- map changes ship with the checksum gate above
- keep `--aniimo.safe` (log-only) and `nopatch` (no code-page detours) modes
  as engine flags; safe-by-default on the hook layer
