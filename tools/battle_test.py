#!/usr/bin/env python3
"""Private-input regression runner. Never writes to the game or kit."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def run(command, logfile=None):
    result = subprocess.run([str(x) for x in command], cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if logfile:
        Path(logfile).write_text(result.stdout)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {' '.join(map(str, command))}\n{result.stdout[-6000:]}")
    return result.stdout


def check_branding(build):
    # Old strings are allowed only for explicit compatibility and the real
    # upstream dependency endpoint. Assembly/type names are checked separately.
    allowed = ["MelonLoader.UnityDependencies", "melonloader.", "MelonPreferences.cfg", "MelonStartScreen.cfg", "MelonLoader"]
    owned = []
    for path in build.rglob("*.dll"):
        if path.name == "version.dll" or path.name.startswith("AniiMod") or any(p in path.parts for p in ("SupportModules", "CompatibilityLayers", "Il2CppAssemblyGenerator")):
            owned.append(path)
    if not owned:
        raise RuntimeError("No loader binaries found for branding validation")
    for path in build.rglob("*"):
        if path.is_file() and "melonloader" in path.name.lower():
            raise RuntimeError(f"Legacy output filename: {path}")
    failures = []
    for path in owned:
        data = path.read_bytes()
        for encoding in ("ascii", "utf-16-le"):
            # Match a whole legacy compatibility string, not a prefix such as
            # MelonLoader.Bootstrap or a sentence/banner containing the brand.
            strings = re.findall(rb"[\x20-\x7e]{4,}", data) if encoding == "ascii" else re.findall(rb"(?:[\x20-\x7e]\x00){4,}", data)
            for raw in strings:
                value = raw.decode(encoding)
                if "melonloader" not in value.lower():
                    continue
                if value in allowed or ("https://github.com/LavaGang/MelonLoader.UnityDependencies/releases/download/" in value):
                    continue
                # User-string length bytes can prefix a UTF-16 payload by one printable character.
                if encoding == "utf-16-le" and len(value) > 1 and value[1:] in allowed:
                    continue
                failures.append(f"{path.relative_to(build)}: {value[:180]}")
    if failures:
        raise RuntimeError("Legacy binary strings:\n" + "\n".join(failures[:30]))
    return len(owned)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--kit", type=Path, help="Private directory with Il2CppNameMap.txt and PrebakedDummyDll")
    parser.add_argument("--input", type=Path, help="Recovered assemblies when the original kit is unavailable; kit gate is reported not run")
    parser.add_argument("--game", type=Path)
    parser.add_argument("--build", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    parser.add_argument("--checks-only", action="store_true", help="Compatibility and branding checks without private game inputs")
    args = parser.parse_args()
    out = args.output.resolve()
    protected = [p.resolve() for p in (args.kit, args.input, args.game) if p]
    if any(out == p or p in out.parents for p in protected):
        raise RuntimeError("Output must be outside game and input directories")
    out.mkdir(parents=True, exist_ok=True)
    report = {"kit": "not run", "generation": "not run", "live_boot": "not run (tester machine)", "branding": "not run"}
    try:
        run([args.dotnet, "build", "tools/Aniimo.InteropDiagnostics", "-c", "Release", "--nologo"], out / "build.log")
        dll = ROOT / "tools/Aniimo.InteropDiagnostics/bin/Release/net6.0/Aniimo.InteropDiagnostics.dll"
        report["compatibility"] = run([args.dotnet, dll, "--self-test"], out / "compatibility.log").strip()
        if args.build:
            report["branding"] = f"passed: {check_branding(args.build.resolve())} loader-owned binaries"
            run([args.dotnet, dll, args.build.resolve() / "AniiModLoader/net6", "unused", "unused", "--assert-branding"], out / "metadata-branding.log")
        if not args.checks_only:
            source = args.input
            if args.kit:
                kit = args.kit.resolve()
                maps = list(kit.rglob("Il2CppNameMap.txt"))
                inputs = [p for p in kit.rglob("PrebakedDummyDll") if p.is_dir()]
                if len(maps) != 1 or len(inputs) != 1:
                    raise RuntimeError("Kit must contain exactly one name map and PrebakedDummyDll directory")
                entries = [line.strip().split("=", 1) for line in maps[0].read_text().splitlines() if line.strip() and not line.lstrip().startswith("#")]
                if len(entries) != 232 or any(len(e) != 2 or not all(e) for e in entries) or len({e[0].lower() for e in entries}) != 232:
                    raise RuntimeError("Name map must contain 232 unique real=scrambled entries")
                source = inputs[0]
                if len(list(source.glob("*.dll"))) != 133:
                    raise RuntimeError("PrebakedDummyDll must contain 133 DLL files")
                for metadata in kit.rglob("*metadata*.dat"):
                    with metadata.open("rb") as f:
                        if f.read(4) != bytes.fromhex("af1bb1fa"):
                            raise RuntimeError(f"Invalid decrypted metadata sanity: {metadata}")
                report["kit"] = "passed"
            if source is None or args.game is None:
                raise RuntimeError("Generation requires --kit or --input, plus --game")
            source = source.resolve()
            game_assembly = args.game.resolve() / "GameAssembly.dll"
            if not game_assembly.is_file():
                raise RuntimeError("GameAssembly.dll missing")
            report["game_sha256"] = hashlib.sha256(game_assembly.read_bytes()).hexdigest()
            normalized = out / "normalized-input"
            run([args.dotnet, dll, "--sanitize", source, normalized], out / "sanitize.log")
            run([args.dotnet, dll, normalized, game_assembly, out / "scan", "--scan"], out / "scan.log")
            inventories = []
            for mode in ("sequential", "parallel", "parallel-repeat"):
                target = out / mode
                if target.exists() and any(target.iterdir()):
                    raise RuntimeError(f"Use a fresh output directory; refusing stale generation outputs: {target}")
                command = [args.dotnet, dll, normalized, game_assembly, target, "--no-xref"]
                if mode != "sequential":
                    command.append("--parallel")
                log = run(command, out / f"{mode}.log")
                if "GENERATION OK" not in log or len(list(target.glob("*.dll"))) < 130:
                    raise RuntimeError(f"Incomplete generation: {mode}")
                for name in ("Il2Cppmscorlib.dll", "Il2CppFunPlus.WorldX.Scripts.dll"):
                    if not (target / name).is_file():
                        raise RuntimeError(f"Missing required Aniimo assembly: {name}")
                inventory = run([args.dotnet, dll, target, "unused", "unused", "--inventory"], out / f"{mode}-inventory.txt")
                inventories.append(hashlib.sha256(inventory.encode()).hexdigest())
            if len(set(inventories)) != 1:
                raise RuntimeError("Sequential and parallel API inventories differ")
            report["generation"] = "passed: sequential and two parallel runs; matching API inventories"
        report["result"] = "passed requested checks"
        print(json.dumps(report, indent=2))
    except Exception as error:
        report["result"] = "failed"
        report["error"] = str(error)
        raise
    finally:
        (out / "report.json").write_text(json.dumps(report, indent=2) + "\n")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"REGRESSION FAILED: {error}", file=sys.stderr)
        sys.exit(1)
