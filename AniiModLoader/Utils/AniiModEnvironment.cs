using System.IO;
using System.Diagnostics;

namespace AniiModLoader.Utils
{
    public static class AniiModEnvironment
    {
        private const string OurRuntimeName =
#if !NET6_0
            "net35";
#else
            "net6";
#endif

        public static bool IsDotnetRuntime { get; } = OurRuntimeName == "net6";
        public static bool IsMonoRuntime { get; } = !IsDotnetRuntime;

        public static string AniiModBaseDirectory => LoaderConfig.Current.Loader.BaseDirectory;

        public static string GameExecutablePath { get; } =
#if OSX
            AniiModUtils.GetPathAncestor(Process.GetCurrentProcess()!.MainModule!.FileName, 3);
#else
            Process.GetCurrentProcess().MainModule.FileName;
#endif
        public static string AniiModLoaderDirectory { get; } = AniiModLoader.Compatibility.LoaderPaths.DataDirectory(AniiModBaseDirectory);
        public static string GameRootDirectory { get; } = Path.GetDirectoryName(GameExecutablePath);


        public static string DependenciesDirectory { get; } = Path.Combine(AniiModLoaderDirectory, "Dependencies");
        public static string SupportModuleDirectory { get; } = Path.Combine(DependenciesDirectory, "SupportModules");
        public static string CompatibilityLayerDirectory { get; } = Path.Combine(DependenciesDirectory, "CompatibilityLayers");
        public static string Il2CppAssemblyGeneratorDirectory { get; } = Path.Combine(DependenciesDirectory, "Il2CppAssemblyGenerator");
        public static string ModsDirectory { get; } = Path.Combine(AniiModBaseDirectory, "Mods");
        public static string PluginsDirectory { get; } = Path.Combine(AniiModBaseDirectory, "Plugins");
        public static string UserLibsDirectory { get; } = Path.Combine(AniiModBaseDirectory, "UserLibs");
        public static string UserDataDirectory { get; } = Path.Combine(AniiModBaseDirectory, "UserData");
        public static string AniiModLoaderLogsDirectory { get; } = Path.Combine(AniiModLoaderDirectory, "Logs");
        public static string OurRuntimeDirectory { get; } = Path.Combine(AniiModLoaderDirectory, OurRuntimeName);

        public static string GameExecutableName { get; } = Path.GetFileNameWithoutExtension(GameExecutablePath);
        public static string UnityGameDataDirectory { get; } = 
#if OSX
            Path.Combine(GameExecutablePath!, "Contents/Resources/Data");
#else
            Path.Combine(GameRootDirectory, GameExecutableName + "_Data");
#endif
        public static string UnityGameManagedDirectory { get; } = Path.Combine(UnityGameDataDirectory, "Managed");
        public static string Il2CppDataDirectory { get; } = Path.Combine(UnityGameDataDirectory, "il2cpp_data");
        public static string UnityPlayerPath { get; } = Path.Combine(GameRootDirectory, "UnityPlayer.dll");

        public static string AniiModManagedDirectory { get; } = Path.Combine(DependenciesDirectory, "Mono");
        public static string Il2CppAssembliesDirectory { get; } = Path.Combine(AniiModLoaderDirectory, "Il2CppAssemblies");

        internal static void PrintEnvironment()
        {
            //These must not be changed, lum needs them
            AniiModLogger.MsgDirect($"Core::BasePath = {AniiModBaseDirectory}");
            AniiModLogger.MsgDirect($"Game::BasePath = {GameRootDirectory}");
            AniiModLogger.MsgDirect($"Game::DataPath = {UnityGameDataDirectory}");
            AniiModLogger.MsgDirect($"Game::ApplicationPath = {GameExecutablePath}");

            AniiModLogger.MsgDirect($"Runtime Type: {OurRuntimeName}");
        }
    }
}