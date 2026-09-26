// Aniimo addition: crash-proof managed trace, mirrors the bootstrap's
// Trace.log so native + managed stages land in one file.

using System;
using System.IO;
using System.Text;

namespace AniiModLoader.InternalUtils
{
    internal static class AniimoTrace
    {
        private static readonly object Gate = new();
        private static bool _failed;

        internal static void Log(string message)
        {
            if (!LoaderConfig.Current.Loader.DebugMode || _failed)
                return;
            try
            {
                lock (Gate)
                {
                    var path = Path.Combine(
                        AniiModLoader.Utils.AniiModEnvironment.AniiModLoaderDirectory,
                        "AniiModTrace.log");
                    File.AppendAllText(path,
                        $"[{DateTime.Now:HH:mm:ss.fff}] [managed] {message}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
            catch
            {
                _failed = true;
            }
        }
    }
}
