using AniiModLoader.Bootstrap.Logging;
using AniiModLoader.Logging;

namespace AniiModLoader.Bootstrap;

internal static class AniiModDebug
{
    private static readonly InternalLogger logger = new(ColorARGB.CornflowerBlue, "BS DEBUG");

    public static void Log(string msg)
    {
        if (!LoaderConfig.Current.Loader.DebugMode)
            return;

        logger.Msg(msg);
    }
}
