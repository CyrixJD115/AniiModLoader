using System;
using System.IO;
using System.Threading.Tasks;
using AniiModLoader.Compatibility;
internal static class CompatibilityTests
{
    internal static void Run()
    {
        int assertions = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); assertions++; }
        foreach (string mode in new[] { "aniimo.safe", "aniimo.earlystart", "aniimo.nopatch" })
        {
            var flags = new LaunchArgumentReader(new[] { "game", "--" + mode.ToUpperInvariant() });
            Check(flags.IsDefined(mode), mode);
            Check(!flags.IsDefined("aniimo.unrelated"), "unrelated flag");
        }
        var mixed = new LaunchArgumentReader(new[] { "game", "--melonloader.debug", "--aniimod.basedir", "path with spaces", "--melonloader.maxlogs=12", "--aniimo.earlystart", "--aniimo.nopatch" });
        Check(mixed.IsDefined("aniimod.debug"), "legacy debug alias");
        Check(mixed.GetValue("MELONLOADER.BASEDIR") == "path with spaces", "new flag queried using old alias");
        Check(mixed.GetValue("aniimod.maxlogs") == "12", "equals value");
        Check(mixed.IsDefined("aniimo.earlystart") && mixed.IsDefined("aniimo.nopatch"), "combined boot mode");
        var duplicate = new LaunchArgumentReader(new[] { "game", "--melonloader.maxlogs=1", "--aniimod.maxlogs=2" });
        Check(duplicate.GetValue("aniimod.maxlogs") == "1", "first flag wins");
        string root = Path.Combine(Path.GetTempPath(), "aniimod-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string legacy = Path.Combine(root, "MelonLoader");
            string current = Path.Combine(root, "AniiModLoader");
            Directory.CreateDirectory(legacy);
            Check(LoaderPaths.DataDirectory(root) == legacy, "legacy-only folder fallback");
            File.WriteAllText(Path.Combine(legacy, "Il2CppNameMap.txt"), "il2cpp_init=ObFuScAtEd\nil2cpp_shutdown=Shutdown\n# comment\ninvalid\n");
            Directory.CreateDirectory(current);
            Check(LoaderPaths.DataDirectory(root) == current, "new folder precedence");
            string mapPath = LoaderPaths.ExistingDataPath(root, "Il2CppNameMap.txt");
            Check(mapPath.StartsWith(legacy, StringComparison.Ordinal), "private-artifact fallback");
            var map = new Il2CppExportNameMap(mapPath);
            File.Delete(mapPath);
            Check(map.Count == 2, "map entries");
            Check(map.Resolve("IL2CPP_INIT") == "ObFuScAtEd", "case-insensitive forward map");
            Check(map.Original("obfuscated") == "il2cpp_init", "case-insensitive reverse map");
            Parallel.For(0, 10000, _ => { if (map.Resolve("il2cpp_init") != "ObFuScAtEd") throw new Exception("Concurrent map lookup"); });
            Check(map.Resolve("missing") == "missing", "unknown export pass-through");
            File.WriteAllText(Path.Combine(root, "Loader.cfg"), "old-data");
            string config = LoaderPaths.MigrateConfig(root, "AniiModLoader.cfg", "Loader.cfg");
            Check(File.ReadAllText(config) == "old-data", "config copy");
            Check(File.ReadAllText(Path.Combine(root, "Loader.cfg")) == "old-data", "original preserved");
            File.WriteAllText(config, "new-data");
            LoaderPaths.MigrateConfig(root, "AniiModLoader.cfg", "Loader.cfg");
            Check(File.ReadAllText(config) == "new-data", "new config never overwritten");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"COMPATIBILITY TESTS OK: {assertions} assertions and 10000 concurrent map lookups");
    }
}
