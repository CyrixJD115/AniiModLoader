using System.Diagnostics;
using AniiModLoader.Logging;
using AniiModLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using AniiModLoader.Bootstrap.RuntimeHandlers.Mono;
using AniiModLoader.Bootstrap.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AniiModLoader.Bootstrap.Logging;
using Tomlet;

namespace AniiModLoader.Bootstrap;

public static class Core
{
    internal static InternalLogger Logger { get; private set; } = new(ColorARGB.BlueViolet, "AniiModLoader.Bootstrap");
    internal static InternalLogger PlayerLogger { get; private set; } = new(ColorARGB.Turquoise, "UNITY");

    [RequiresDynamicCode("Calls InitConfig")]
    public static void Init()
    {
        LoaderConfig.Initialize();

        if (LoaderConfig.Current.Loader.Disable)
            return;

        AniiModLogger.Init();
        if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
            ConsoleHandler.NullHandles();

        ModuleSymbolRedirect.Attach();
    }
}
