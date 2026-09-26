using System.Runtime.InteropServices;
using Tomlet.Attributes;

#if BOOTSTRAP
using Tomlet;
using AniiModLoader.Bootstrap.Utils;
using System.Diagnostics.CodeAnalysis;
#endif

namespace AniiModLoader;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class LoaderConfig
{
#if BOOTSTRAP

#if OSX
    private static string GetParentDirectory(string path, int level)
    {
        string parentPath = path;
        for (int i = 0; i < level; i++)
            parentPath = Path.GetDirectoryName(parentPath)!;
        return parentPath;
    }
#endif

    [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
    internal static void Initialize()
    {
        var customBaseDir = ArgParser.GetValue("aniimod.basedir");
        var baseDir = Path.GetDirectoryName(Environment.ProcessPath)!;

#if OSX
        baseDir = GetParentDirectory(baseDir, 3);
#endif

        if (Directory.Exists(customBaseDir))
            baseDir = Path.GetFullPath(customBaseDir);

        var userDataFolder = Path.Combine(baseDir, "UserData");
        if (!Directory.Exists(userDataFolder))
            Directory.CreateDirectory(userDataFolder);

        var path = Compatibility.LoaderPaths.MigrateConfig(userDataFolder, "AniiModLoader.cfg", "Loader.cfg");
        if (File.Exists(path))
        {
            try
            {
                var doc = TomlParser.ParseFile(path);
                Current = TomletMain.To<LoaderConfig>(doc) ?? new LoaderConfig();
                SaveFile(path);
            }
            catch
            {
            }
        }
        else
            TrySaveFile(path);

        CoreConfig.Initialize(baseDir);
        ConsoleConfig.Initialize();
        LogsConfig.Initialize();
        MonoDebugServerConfig.Initialize();
        UnityEngineConfig.Initialize();
    }

    private static void TrySaveFile(string path)
    {
        try
        {
            SaveFile(path);
        }
        catch
        {
        }
    }
    
    private static void SaveFile(string path)
    {
        var doc2 = TomletMain.TomlStringFrom(Current);
        File.WriteAllText(path, doc2);
    }
    
#endif

    public static LoaderConfig Current { get; internal set; } = new();

    [TomlProperty("loader")]
    public CoreConfig Loader { get; internal set; } = new();

    [TomlProperty("console")]
    public ConsoleConfig Console { get; internal set; } = new();

    [TomlProperty("logs")]
    public LogsConfig Logs { get; internal set; } = new();

    [TomlProperty("mono_debug_server")]
    public MonoDebugServerConfig MonoDebugServer { get; internal set; } = new();

    [TomlProperty("unityengine")]
    public UnityEngineConfig UnityEngine { get; internal set; } = new();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class CoreConfig
    {
#if BOOTSTRAP
        [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
        internal static void Initialize(string baseDir)
        {
            Current.Loader.BaseDirectory = baseDir;

#if !DEBUG
            if (ArgParser.IsDefined("aniimod.debug"))
#endif
                Current.Loader.DebugMode = true;

            if (ArgParser.IsDefined("aniimod.captureplayerlogs"))
                Current.Loader.CapturePlayerLogs = true;

            if (int.TryParse(ArgParser.GetValue("aniimod.harmonyloglevel"), out var harmonyLogLevel))
                Current.Loader.HarmonyLogLevel = (HarmonyLogVerbosity)Math.Clamp(harmonyLogLevel, (int)HarmonyLogVerbosity.None, (int)HarmonyLogVerbosity.IL);

            if (ArgParser.IsDefined("no-mods"))
                Current.Loader.Disable = true;

            if (ArgParser.IsDefined("quitfix"))
                Current.Loader.ForceQuit = true;

            if (ArgParser.IsDefined("aniimod.disablestartscreen"))
                Current.Loader.DisableStartScreen = true;

            if (ArgParser.IsDefined("aniimod.launchdebugger"))
                Current.Loader.LaunchDebugger = true;

            if (int.TryParse(ArgParser.GetValue("aniimod.consolemode"), out var valueint))
                Current.Loader.Theme = (LoaderTheme)Math.Clamp(valueint, (int)LoaderTheme.Normal, (int)LoaderTheme.Lemon);

            if (ArgParser.IsDefined("aniimod.nosfload"))
                Current.Loader.DisableSubFolderLoad = true;

            if (ArgParser.IsDefined("aniimod.nosfmanifest"))
                Current.Loader.DisableSubFolderManifest = true;

            string? hostfxrPath = ArgParser.GetValue("aniimod.hostfxr");
            if (hostfxrPath != null)
                Current.Loader.HostFXRPathOverride = hostfxrPath;
        }
#endif

            [TomlNonSerialized]
        public string BaseDirectory { get; internal set; } = null!;

        // Technically, this will always return false, but it's still a config ¯\_(ツ)_/¯
        [TomlProperty("disable")]
        [TomlPrecedingComment("Disables AniiModLoader. Equivalent to the '--no-mods' launch option")]
        public bool Disable { get; internal set; }

        [TomlProperty("debug_mode")]
        [TomlPrecedingComment("Equivalent to the '--aniimod.debug' launch option")]
        public bool DebugMode { get; internal set; }

        [TomlProperty("capture_player_logs")]
        [TomlPrecedingComment("Capture all Unity player logs into AniiModLoader's logs even if the game disabled them. NOTE: Depending on the game or Unity version, these logs can be overly verbose. Equivalent to the '--aniimod.captureplayerlogs' launch option")]
        public bool CapturePlayerLogs { get; internal set; }

        [TomlProperty("harmony_log_level")]
        [TomlPrecedingComment("The maximum Harmony log verbosity to capture into AniiModLoader's logs. Possible values in verbosity order are: \"None\", \"Error\", \"Warn\", \"Info\", \"Debug\", or \"IL\". Equivalent to the '--aniimod.harmonyloglevel' launch option")]
        public HarmonyLogVerbosity HarmonyLogLevel { get; internal set; } = HarmonyLogVerbosity.Warn;

        [TomlProperty("force_quit")]
        [TomlPrecedingComment("Only use this if the game freezes when trying to quit. Equivalent to the '--quitfix' launch option")]
        public bool ForceQuit { get; internal set; }

        [TomlProperty("disable_start_screen")]
        [TomlPrecedingComment("Disables the start screen. Equivalent to the '--aniimod.disablestartscreen' launch option")]
        public bool DisableStartScreen { get; internal set; }

        [TomlProperty("launch_debugger")]
        [TomlPrecedingComment("Starts the dotnet debugger on Windows and wait it is attached or just wait until one is attached without launch on other OSes (only for Il2Cpp games). Equivalent to the '--aniimod.launchdebugger' launch option")]
        public bool LaunchDebugger { get; internal set; }

        [TomlProperty("theme")]
        [TomlPrecedingComment("Sets the loader theme. Currently, the only available themes are \"Normal\" and \"Lemon\". Equivalent to the '--aniimod.consolemode' launch option (0 for Normal, 4 for Lemon)")]
        public LoaderTheme Theme { get; internal set; }

        [TomlProperty("disable_subfolder_load")]
        [TomlPrecedingComment("Disables the loading of AniiMod Subfolders. Equivalent to the '--aniimod.nosfload' launch option")]
        public bool DisableSubFolderLoad { get; internal set; }

        [TomlProperty("disable_subfolder_manifest")]
        [TomlPrecedingComment("Disables the requirement of needing a manifest json inside a AniiMod Subfolder for it to be loaded. Equivalent to the '--aniimod.nosfmanifest' launch option")]
        public bool DisableSubFolderManifest { get; internal set; }

        [TomlProperty("hostfxr_path_override")]
        [TomlPrecedingComment("Manually defines the HostFXR path to use for DotNet Initialization. Equivalent to the '--aniimod.hostfxr' launch option")]
        public string HostFXRPathOverride { get; internal set; } = "";

        public enum LoaderTheme
        {
            Normal,
            Magenta,
            Rainbow,
            RandomRainbow,
            Lemon
        }

        public enum HarmonyLogVerbosity
        {
            None,
            Info,
            Warn,
            Error,
            Debug,
            IL
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class ConsoleConfig
    {
#if BOOTSTRAP
        [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
        internal static void Initialize()
        {
            if (ArgParser.IsDefined("aniimod.hideconsole"))
                Current.Console.Hide = true;

            if (ArgParser.IsDefined("aniimod.consoleontop"))
                Current.Console.AlwaysOnTop = true;

            if (ArgParser.IsDefined("aniimod.consoledst"))
                Current.Console.DontSetTitle = true;

            if (ArgParser.IsDefined("aniimod.hidewarnings"))
                Current.Console.HideWarnings = true;
        }
#endif

        [TomlProperty("hide_warnings")]
        [TomlPrecedingComment("Hides warnings from displaying. Equivalent to the '--aniimod.hidewarnings' launch option")]
        public bool HideWarnings { get; internal set; }

        [TomlProperty("hide_console")]
        [TomlPrecedingComment("Hides the console. Equivalent to the '--aniimod.hideconsole' launch option")]
        public bool Hide { get; internal set; }

        [TomlProperty("console_on_top")]
        [TomlPrecedingComment("Forces the console to always stay on-top of all other applications. Equivalent to the '--aniimod.consoleontop' launch option")]
        public bool AlwaysOnTop { get; internal set; }

        [TomlProperty("dont_set_title")]
        [TomlPrecedingComment("Keeps the console title as original. Equivalent to the '--aniimod.consoledst' launch option")]
        public bool DontSetTitle { get; internal set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class LogsConfig
    {
#if BOOTSTRAP
        [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
        internal static void Initialize()
        {
            if (uint.TryParse(ArgParser.GetValue("aniimod.maxlogs"), out var maxLogs))
                Current.Logs.MaxLogs = maxLogs;
        }
#endif

        [TomlProperty("max_logs")]
        [TomlPrecedingComment("Sets the maximum amount of log files in the Logs folder (Default: 10). Equivalent to the '--aniimod.maxlogs' launch option")]
        public uint MaxLogs { get; internal set; } = 10;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class MonoDebugServerConfig
    {
#if BOOTSTRAP
        [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
        internal static void Initialize()
        {
            if (ArgParser.IsDefined("aniimod.debugsuspend"))
                Current.MonoDebugServer.DebugSuspend = true;

            var debugIpAddress = ArgParser.GetValue("aniimod.debugipaddress");
            if (debugIpAddress != null)
                Current.MonoDebugServer.DebugIpAddress = debugIpAddress;

            if (uint.TryParse(ArgParser.GetValue("aniimod.debugport"), out var debugPort))
                Current.MonoDebugServer.DebugPort = debugPort;
        }
#endif

        [TomlProperty("debug_suspend")]
        [TomlPrecedingComment("Let the Mono debug server wait until a debugger is attached when debug_mode is true (only for Mono games). Equivalent to the '--aniimod.debugsuspend' launch option")]
        public bool DebugSuspend { get; internal set; }

        [TomlProperty("debug_ip_address")]
        [TomlPrecedingComment("The IP address the Mono debug server will listen to when debug_mode is true (only for Mono games). Equivalent to the '--aniimod.debugipaddress' launch option")]
        public string DebugIpAddress { get; internal set; } = "127.0.0.1";

        [TomlProperty("debug_port")]
        [TomlPrecedingComment("The port the Mono debug server will listen to when debug_mode is true (only for Mono games). Equivalent to the '--aniimod.debugport' launch option")]
        public uint DebugPort { get; internal set; } = 55555;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class UnityEngineConfig
    {
#if BOOTSTRAP
        [RequiresDynamicCode("Dynamically accesses LoaderConfig properties")]
        internal static void Initialize()
        {
            var unityVersionOverride = ArgParser.GetValue("aniimod.unityversion");
            if (unityVersionOverride != null)
                Current.UnityEngine.VersionOverride = unityVersionOverride;

            if (ArgParser.IsDefined("aniimod.disableunityclc"))
                Current.UnityEngine.DisableConsoleLogCleaner = true;

            var monoSearchPathOverride = ArgParser.GetValue("aniimod.monosearchpathoverride");
            if (monoSearchPathOverride != null)
                Current.UnityEngine.MonoSearchPathOverride = monoSearchPathOverride;

            if (ArgParser.IsDefined("aniimod.agfregenerate"))
                Current.UnityEngine.ForceRegeneration = true;

            if (ArgParser.IsDefined("aniimod.mbepatch"))
                Current.UnityEngine.MBEPatch = true;

            if (ArgParser.IsDefined("aniimod.agfoffline"))
                Current.UnityEngine.ForceOfflineGeneration = true;

            var forceRegex = ArgParser.GetValue("aniimod.agfregex");
            if (forceRegex != null)
                Current.UnityEngine.ForceGeneratorRegex = forceRegex;

            var forceDumperVersion = ArgParser.GetValue("aniimod.agfvdumper");
            if (forceDumperVersion != null)
                Current.UnityEngine.ForceIl2CppDumperVersion = forceDumperVersion;

            if (ArgParser.IsDefined("cpp2il.callanalyzer"))
                Current.UnityEngine.EnableCpp2ILCallAnalyzer = true;

            if (ArgParser.IsDefined("cpp2il.nativemethoddetector"))
                Current.UnityEngine.EnableCpp2ILNativeMethodDetector = true;
        }
#endif

        [TomlNonSerialized]
        private const string MonoPathSeparatorDescription =
#if WINDOWS
            "semicolon (;)";
#elif LINUX || OSX
            "colon (:)";
#endif
        
        [TomlProperty("version_override")]
        [TomlPrecedingComment("Overrides the detected UnityEngine version. Equivalent to the '--aniimod.unityversion' launch option")]
        public string VersionOverride { get; internal set; } = "";

        [TomlProperty("disable_console_log_cleaner")]
        [TomlPrecedingComment("Disables the console log cleaner (only applies to Il2Cpp games). Equivalent to the '--aniimod.disableunityclc' launch option")]
        public bool DisableConsoleLogCleaner { get; internal set; }

        [TomlProperty("mono_search_path_override")]
        [TomlPrecedingComment($"A {MonoPathSeparatorDescription} separated list of paths that Mono will prioritise to seek mscorlib and core libraries before the Managed folder and AniiMod's included set of core libraries. Equivalent to the '--aniimod.monosearchpathoverride' launch option")]
        public string MonoSearchPathOverride { get; internal set; } = "";

        [TomlProperty("mono_bleeding_edge_environment_patches")]
        [TomlPrecedingComment("Forces MonoBleedingEdge to utilize included Environment Patches. Equivalent to the '--aniimod.mbepatch' launch option")]
        public bool MBEPatch { get; internal set; }

        [TomlProperty("force_offline_generation")]
        [TomlPrecedingComment("Forces the Il2Cpp Assembly Generator to run without contacting the remote API. Equivalent to the '--aniimod.agfoffline' launch option")]
        public bool ForceOfflineGeneration { get; internal set; }

        [TomlProperty("force_generator_regex")]
        [TomlPrecedingComment("Forces the Il2Cpp Assembly Generator to use the specified regex. Equivalent to the '--aniimod.agfregex' launch option")]
        public string ForceGeneratorRegex { get; internal set; } = "";

        [TomlProperty("force_il2cpp_dumper_version")]
        [TomlPrecedingComment("Forces the Il2Cpp Assembly Generator to use the specified Il2Cpp dumper version. Equivalent to the '--aniimod.agfvdumper' launch option")]
        public string ForceIl2CppDumperVersion { get; internal set; } = "";

        [TomlProperty("force_regeneration")]
        [TomlPrecedingComment("Forces the Il2Cpp Assembly Generator to always regenerate assemblies. Equivalent to the '--aniimod.agfregenerate' launch option")]
        public bool ForceRegeneration { get; internal set; }

        [TomlProperty("enable_cpp2il_call_analyzer")]
        [TomlPrecedingComment("Enables the CallAnalyzer processor for Cpp2IL. Equivalent to the '--cpp2il.callanalyzer' launch option")]
        public bool EnableCpp2ILCallAnalyzer { get; internal set; }

        [TomlProperty("enable_cpp2il_native_method_detector")]
        [TomlPrecedingComment("Enables the NativeMethodDetector processor for Cpp2IL. Equivalent to the '--cpp2il.nativemethoddetector' launch option")]
        public bool EnableCpp2ILNativeMethodDetector { get; internal set; }
    }
}
