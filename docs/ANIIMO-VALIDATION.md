# Aniimo generation validation — 2026-09-26

Base: `aniimo-support`, `c8ada204b0fe4ed8d6dd69961b728bb6cb008178`.
All existing symbol translation, early-start, no-patch and Wine guards remain.

## Reproduction and repairs

The installed Windows GameAssembly SHA-256 is
`911e8c08fe17cd5ec6d16105e84f8f0d15bfb270abd4d05baaa4337bbfb96e2b`.
Its encrypted metadata SHA-256 is
`40787a67b935eafd5797886a4fdd4469276bce76499b1b28951bccf5134f05ae`.
No game binaries or metadata were changed.

The previous machine's PrebakedDummyDll and export-name map were not present.
A fresh dump was produced locally using the existing Apiimo metadata recovery
library and CyrixJD115/Il2CppDumper at a261ffc. Metadata recovery reports 133
images, 224298 sanitized default records, and one malformed generic container.
Dumper attribute-restoration warnings remain; generated wrappers are not yet
runtime-validated. Game-derived inputs are deliberately excluded from Git.

The dump contains 133 files ending in .dll (including the Il2CppDummyDll helper)
and one extensionless managed assembly, `__Generated`. Filtering for .dll files
reproduces the reported Pass12 NullReferenceException, even sequentially with
an explicit ModuleReaderParameters directory.

The unresolved reference is:

```
FunPlus.WorldX.SDK.Platform.WindowsSystemKeyboardBridge/UIHostNoLaunch
  -> System.__Il2CppComObject, assembly __Generated
```

Copying `__Generated` as `__Generated.dll` restores the real type. No substitute
base class or fabricated mscorlib stub is needed. The loader now normalizes the
prebaked input into its working output folder and never modifies the originals.

Two later failures become visible after Pass12:

* The optional xref cache's metadata-init pattern scanner fails with
  `Sequence contains no elements`. Use Il2CppInterop's public `NoXrefCache`
  option for the prebaked path. Ordinary method cross-reference scanning still
  runs. Runtime behavior of mods depending on that optional cache is untested.
* Invalid generic owners trigger Pass18 `ArgumentOutOfRangeException`.
  Working-copy normalization repairs 29 generic parameter references where the
  declaring type has no parameters and the method owns the referenced index.
  One `<>f__AnonymousType0`1` has its one declared type parameter restored.
  These repairs are structurally justified, but native calls using those
  signatures still require in-game validation.

## Validation status

* Full offline generation: **passed**, one sequential and two parallel runs.
* Reapplying the sanitizer: **zero changes**, all 134 input files byte-identical.
* Output: **133 interop DLLs**, with identical type/field/method inventories
  between sequential and parallel runs. Binary hashes differ between runs;
  byte-for-byte reproducibility is not claimed.
* Required actual-build assemblies include `Il2CppFunPlus.WorldX.Scripts.dll`,
  `Assembly-CSharp-firstpass.dll`, `Il2Cppmscorlib.dll`, and
  `Il2Cpp__Generated.dll`. This input build has no `Assembly-CSharp.dll`.
* Base-type resolution scan after normalization: **zero unresolved**.
* Managed loader and generator Windows-targeted build: **passed**.
* Windows NativeAOT bootstrap: **passed on Windows CI** at
  https://github.com/CyrixJD115/AniiModLoader/actions/runs/36272298004.
  Direct macOS cross-compilation was rejected; CI built the actual native DLL.
* Main-menu boot, second-boot hash cache, safe mode, test-mod load: **not run**.
* Export-name map: **not recovered/verified**. Do not substitute guessed names.
* Branding rename and optimizations gated on boot validation: **pending**.

The official AniiModLoader 0.7.3 x64 archive was downloaded and verified against
GitHub's SHA-256 digest, but does not replace this fork or supply game-derived
artifacts. No upstream bootstrap was deployed into the game.

## Reproduce with .NET 6 runtime and a current SDK

From the repository root (quote paths containing spaces):

```sh
dotnet build tools/Aniimo.InteropDiagnostics -c Release
DIAG=tools/Aniimo.InteropDiagnostics/bin/Release/net6.0/Aniimo.InteropDiagnostics.dll
dotnet "$DIAG" --sanitize /path/to/raw-dump/DummyDll /path/to/working-input
dotnet "$DIAG" /path/to/working-input /path/to/GameAssembly.dll /path/to/output --scan
dotnet "$DIAG" /path/to/working-input /path/to/GameAssembly.dll /path/to/output --no-xref
dotnet "$DIAG" /path/to/working-input /path/to/GameAssembly.dll /path/to/parallel-output --no-xref --parallel
```

The tool links the same sanitizer source as the loader. `--inventory` emits
sorted type, field, and method declarations for output comparison. Keep raw
inputs and output directories separate. Il2CppInterop internals are unchanged.
