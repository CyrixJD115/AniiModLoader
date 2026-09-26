# AniiModLoader changelog

## Rename and optimization development branch

* Rename namespaces, assemblies, projects, mod attributes, branding and runtime
  paths to AniiModLoader. The Windows proxy remains `version.dll`.
* Preserve legacy launch flags and data/config fallback without overwriting old
  files. Mods using the old assembly and namespace must be rebuilt.
* Share tested flag parsing and read-only export-map parsing between runtime
  components. Detailed crash tracing is debug-only.
* Stream the GameAssembly hash once per generation check.
* Add private-data regression tooling and Windows artifact builds.

## Generation repair baseline

* Recover the extensionless `__Generated` assembly when preparing prebaked input.
* Repair malformed generic parameter ownership in working copies only.
* Disable the unsupported optional metadata-init xref cache for prebaked input.
* Verify successful sequential and repeated parallel generation of 133 interop
  assemblies against the installed Aniimo build.

This is a fork; original licensing and attribution are retained in NOTICE.txt.
