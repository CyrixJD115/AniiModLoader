using System;
using System.Collections.Generic;
using System.IO;
namespace AniiModLoader.Compatibility
{
    // Published only after parsing is complete. The dictionaries are read-only
    // after construction, so concurrent symbol lookups need no locks or file I/O.
    internal sealed class Il2CppExportNameMap
    {
        private readonly Dictionary<string, string> forward = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal int Count { get { return forward.Count; } }
        internal Il2CppExportNameMap(string path)
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                string real = line.Substring(0, separator).Trim();
                string scrambled = line.Substring(separator + 1).Trim();
                if (real.Length > 0 && scrambled.Length > 0) forward[real] = scrambled;
            }
            foreach (KeyValuePair<string, string> pair in forward) reverse[pair.Value] = pair.Key;
        }
        internal string Resolve(string real) { string value; return forward.TryGetValue(real, out value) ? value : real; }
        internal string Original(string scrambled) { string value; return reverse.TryGetValue(scrambled, out value) ? value : scrambled; }
    }
}
