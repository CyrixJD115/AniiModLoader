# AniiModLoader rename validation — 2026-09-26

Branch: `revamp/rename-optimizations`, based on `aniimo-support` at `f92a5428`.
The latest `aniimo-support` was fetched before final regression; it contained
no additional commits to merge. Windows build tested commit: `585ef32f`.

## Changes

- Renamed solution, projects, namespaces, public attributes/types, managed
  assemblies, runtime paths, logs, configuration, console text, and default
  start-screen branding. The native Windows proxy remains `version.dll`.
- Preserved old launch-option aliases, legacy data-folder/private-input fallback,
  and configuration migration without overwriting existing new settings.
- Cached case-insensitive export maps; lookups perform no further file reads.
  Managed map initialization is lazy. GameAssembly hashing streams the file;
  debug traces and per-repair output are gated. Native symbol lookup buffers
  are freed after each lookup.
- Retained parallel generation after repeated comparisons with sequential output.
- Added `tools/battle_test.py` and shared-code compatibility tests; Windows CI
  runs the non-private tests and checks compiled binary branding.

## Verified

- Managed loader, generator and start screen build locally; start screen targets
  both .NET 3.5 and .NET 6.
- Complete native Windows x64 solution build and binary branding checks passed:
  https://github.com/CyrixJD115/AniiModLoader/actions/runs/36273689774
- 21 compatibility assertions and 10,000 concurrent cached name-map lookups pass.
- Final real-input regression: sequential plus two parallel generation runs,
  each producing 133 interop assemblies, with identical sorted API inventories.
  Binary hashes between generation runs are not expected to match.
- Real inputs are the recovered 133 DLLs plus extensionless `__Generated`,
  normalized into 134 working-copy DLLs. The original inputs are unchanged.
- GameAssembly SHA-256:
  `911e8c08fe17cd5ec6d16105e84f8f0d15bfb270abd4d05baaa4337bbfb96e2b`.

## Remaining tester checks

Live main-menu boot, repeat-boot caching, runtime behavior of the three Aniimo
modes, and loading a rebuilt test mod require Windows/Proton. Flag parsing was
tested; live behavior is not claimed. The original 232-entry private kit was
not available, so its strict artifact gate was not run. The regression runner
supports it with `--kit` and reports missing prerequisites explicitly.

No game files or protocol payloads were modified. Private recovered inputs,
interop outputs, and maps are not committed. Rebuild existing mods against
`AniiModLoader.dll` and its renamed attributes before runtime testing.
