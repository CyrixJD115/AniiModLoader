// Aniimo addition: crash-proof trace logger.
//
// Appends + flushes per line so a hard native crash leaves everything up
// to the crash on disk. Path derives from the process location because
// LoaderConfig's BaseDirectory is not populated until Core.Init.

using System.Text;

namespace MelonLoader.Bootstrap;

internal static class Trace
{
    private static readonly object Gate = new();
    private static string? _path;
    private static bool _failed;

    public static void Log(string message)
    {
        if (_failed)
            return;
        try
        {
            lock (Gate)
            {
                if (_path is null)
                {
                    var processPath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(processPath))
                        return;
                    var gameDir = Path.GetDirectoryName(processPath);
                    if (string.IsNullOrEmpty(gameDir))
                        return;
                    _path = Path.Combine(gameDir, "MelonLoader", "Trace.log");
                }
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(_path, line, Encoding.UTF8);
            }
        }
        catch
        {
            _failed = true;
        }
    }
}
