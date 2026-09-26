# AniiModLoader

An Aniimo-focused Unity IL2CPP mod loader, maintained at
[CyrixJD115/AniiModLoader](https://github.com/CyrixJD115/AniiModLoader).
The fork retains its original Apache-2.0 license and upstream attribution in
[LICENSE.md](LICENSE.md) and [NOTICE.txt](NOTICE.txt).

## Installation and compatibility

Use the Windows x64 artifacts from the repository's **Build Aniimo Windows
artifacts** workflow. Install `version.dll` beside `Aniimo.exe` and the complete
`AniiModLoader/` directory alongside it. The Windows proxy must remain named
`version.dll`. Put rebuilt mods in `Mods/`; plugins remain in `Plugins/`.

This release changes the public API: reference `AniiModLoader.dll`, use the
`AniiModLoader` namespace and attributes such as `AniiModInfo`. Existing mods
compiled against the old assembly must be rebuilt. Generated Il2CppInterop
assemblies keep their standard names and format.

Private game-derived files are not distributed by this repository:

* `AniiModLoader/Il2CppNameMap.txt`
* `AniiModLoader/Dependencies/Il2CppAssemblyGenerator/PrebakedDummyDll/`
* Optional `AniimoShadow/` and decrypted metadata, used only for offline tools.

The loader automatically falls back to a legacy `MelonLoader/` directory if
`AniiModLoader/` is absent. When both exist, new binaries take priority and
missing private input artifacts can still be read from the legacy directory.
Legacy files are never deleted or overwritten. Config files are copied to their
new names only when those files do not already exist; an unwritable destination
continues using the old config. Install the complete new binary set even when
reusing a legacy data folder.

Logs use `AniiModLoader.log`. Configuration uses `UserData/AniiModLoader.cfg`,
`UserData/AniiModPreferences.cfg`, and generator `AniiModGenerator.cfg`.

## Launch modes

* `--aniimo.safe`: observation mode, no IL2CPP symbol redirects.
* `--aniimo.earlystart --aniimo.nopatch`: early managed start with the existing
  no-native-detour behavior.
* `--aniimod.debug`: detailed debug and crash-trace logging.

Every legacy `--melonloader.*` option remains accepted as an alias for the
corresponding `--aniimod.*` option, with case-insensitive matching. Both
`--option=value` and `--option value` work. When a flag appears more than once,
the first occurrence wins, matching the original bootstrap behavior.

## Build and regression checks

Use a .NET 9 SDK and .NET 6 runtime. Native Windows builds require Windows and
Visual Studio C++ build tools; the included CI workflow supplies these.

```sh
dotnet build AniiModLoader.sln -c Release -p:Platform=x64 -p:ForceRID=win-x64
python3 tools/battle_test.py --kit /private/kit-directory --game /path/to/Aniimo --build Output/Release/win-x64 --output /private/validation-output
```

The kit directory must contain the export map and PrebakedDummyDll directory.
The test script fails explicitly on absent or invalid prerequisites, tests the
actual shared flag/path/map code, runs sequential and parallel interop generation,
and scans loader-owned binaries for leftover names. Keep its outputs private.
Live main-menu, repeat-boot cache, safe-mode, and test-mod checks belong on the
Windows/Proton tester. Offline success is not a claim that those boots passed.
