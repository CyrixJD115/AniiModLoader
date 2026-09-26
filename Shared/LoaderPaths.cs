using System.IO;

namespace AniiModLoader.Compatibility
{
    internal static class LoaderPaths
    {
        internal const string LegacyDirectoryName = "MelonLoader";
        internal static string DataDirectory(string baseDirectory)
        {
            string current = Path.Combine(baseDirectory, "AniiModLoader");
            string legacy = Path.Combine(baseDirectory, LegacyDirectoryName);
            return !Directory.Exists(current) && Directory.Exists(legacy) ? legacy : current;
        }

        // A new binary installation may coexist with old private artifacts. Resolve
        // those read-only without overwriting either installation or following symlinks.
        internal static string ExistingDataPath(string baseDirectory, string relativePath)
        {
            string current = Path.Combine(DataDirectory(baseDirectory), relativePath);
            if (File.Exists(current) || Directory.Exists(current)) return current;
            string legacy = Path.Combine(Path.Combine(baseDirectory, LegacyDirectoryName), relativePath);
            return File.Exists(legacy) || Directory.Exists(legacy) ? legacy : current;
        }

        internal static string MigrateConfig(string directory, string currentName, string legacyName)
        {
            string current = Path.Combine(directory, currentName);
            string legacy = Path.Combine(directory, legacyName);
            if (!File.Exists(current) && File.Exists(legacy))
            {
                try { File.Copy(legacy, current, false); }
                catch (IOException) { if (!File.Exists(current)) return legacy; }
                catch (System.UnauthorizedAccessException) { return legacy; }
            }
            return current;
        }
    }
}
