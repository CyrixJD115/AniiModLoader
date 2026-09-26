using System;
using System.Collections.Generic;

namespace AniiModLoader.Compatibility
{
    internal sealed class LaunchArgumentReader
    {
        private readonly Dictionary<string, string> arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static class ProcessArguments { internal static readonly LaunchArgumentReader Value = new LaunchArgumentReader(Environment.GetCommandLineArgs()); }
        internal static LaunchArgumentReader Process { get { return ProcessArguments.Value; } }

        internal static string Normalize(string name)
        {
            const string legacy = "melonloader.";
            return name.StartsWith(legacy, StringComparison.OrdinalIgnoreCase) ? "aniimod." + name.Substring(legacy.Length) : name;
        }

        internal LaunchArgumentReader(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("-", StringComparison.Ordinal)) continue;
                arg = arg.Substring(arg.StartsWith("--", StringComparison.Ordinal) ? 2 : 1);
                string value = null;
                int equals = arg.IndexOf('=');
                if (equals >= 0) { value = arg.Substring(equals + 1); arg = arg.Substring(0, equals); }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) value = args[++i];
                arg = Normalize(arg);
                // Preserve the existing bootstrap's first occurrence wins rule.
                if (!arguments.ContainsKey(arg)) arguments.Add(arg, value);
            }
        }
        internal bool IsDefined(string name) { return arguments.ContainsKey(Normalize(name)); }
        internal string GetValue(string name) { string value; return arguments.TryGetValue(Normalize(name), out value) ? value : null; }
    }
}
