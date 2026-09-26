using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using bHapticsLib;
using System.Threading;
using HarmonyLib;
using AniiModLoader.Resolver;
using AniiModLoader.Utils;
using AniiModLoader.InternalUtils;
using AniiModLoader.AniiMods;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;

[assembly: AniiModLoader.PatchShield]

#pragma warning disable IDE0051 // Prevent the IDE from complaining about private unreferenced methods

namespace AniiModLoader
{
    internal static class Core
    {
        private static bool _success = true;

        internal static HarmonyLib.Harmony HarmonyInstance;
        internal static bool Is_ALPHA_PreRelease = false;

        internal static int Initialize()
        {
            // The config should be set before running anything else due to static constructors depending on it
            // Don't ask me how this works, because I don't know either. -slxdy
            var config = new LoaderConfig();
            BootstrapInterop.Library.GetLoaderConfig(ref config);
            LoaderConfig.Current = config;

            AniiModLaunchOptions.Load();

#if NET35
            // Disabled for now because of issues
            //Net20Compatibility.TryInstall();
#endif

            AniiModUtils.SetupWineCheck();

            if (AniiModUtils.IsUnderWineOrSteamProton())
                Pastel.ConsoleExtensions.Disable();

            Fixes.UnhandledException.Install(AppDomain.CurrentDomain);

#if NET35
            Fixes.NetFramework.ServerCertificateValidation.Install();
#endif

            Assertions.LemonAssertMapping.Setup();
            HarmonyLogger.Setup();

#if !WINDOWS && !NET6_0_OR_GREATER
            // Using Process.Start can run Console..cctor
            // Since MonoMod's PlatformHelper (used by DetourHelper.Native) runs Process.Start to determine ARM/x86
            // platform, this causes the unpatched TermInfoReader to kick in before it can be patched and fixed when
            // installing the XTermFix below. To work around this, we can force the platform directly
            DetourHelper.Native = new DetourNativeMonoPosixPlatform(new DetourNativeX86Platform());
#endif

            HarmonyInstance = new HarmonyLib.Harmony(Properties.BuildInfo.Name);

#if !WINDOWS && !NET6_0_OR_GREATER
            Fixes.NetFramework.XTermFix.Install();
#endif

#if OSX
            Fixes.ProcessModulesFix.Install();
#endif

            AniiModUtils.Setup(AppDomain.CurrentDomain);
            AniiModAssemblyResolver.Setup();
            BootstrapInterop.SetDefaultConsoleTitleWithGameName(UnityInformationHandler.GameName, 
                UnityInformationHandler.GameVersion);

#if NET6_0_OR_GREATER

            if (LoaderConfig.Current.Loader.LaunchDebugger && AniiModEnvironment.IsDotnetRuntime)
            {
#if WINDOWS
                AniiModLogger.Msg("[Init] User requested debugger, attempting to launch now...");
                if (Debugger.Launch())
                {
                    AniiModLogger.Msg("[Init] Done interacting with the debugger launch window, waiting for the debugger to be attached...");
                    while (!Debugger.IsAttached)
                    { }
                    AniiModLogger.Msg("[Init] Detected a debugger, resuming initialization...");
                }
#else
                AniiModLogger.Msg("[Init] User requested to wait until a debugger is attached, waiting now...");
                while (!Debugger.IsAttached)
                { }
                AniiModLogger.Msg("[Init] Detected a debugger, resuming initialization...");
#endif
            }

            Environment.SetEnvironmentVariable("IL2CPP_INTEROP_DATABASES_LOCATION", AniiModEnvironment.Il2CppAssembliesDirectory);
            AniiModAssemblyResolver.AddSearchDirectory(AniiModEnvironment.Il2CppAssembliesDirectory);

#else

            try
            {
                if (!MonoLibrary.Setup())
                {
                    _success = false;
                    return 1;
                }
            }
            catch (Exception ex)
            {
                AniiModDebug.Msg($"[MonoLibrary] Caught Exception: {ex}");
                _success = false;
                return 1;
            }

#endif

            Fixes.MonoMod.DetourContextDisposeFix.Install();

#if NET6_0_OR_GREATER
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //  NativeStackWalk.LogNativeStackTrace();

            Fixes.Dotnet.DotnetAssemblyLoadContextFix.Install();
            Fixes.Dotnet.DotnetModHandlerRedirectionFix.Install();
#endif

            Fixes.ForcedCultureInfo.Install();
            Fixes.MonoMod.InstancePatchFix.Install();

#if WINDOWS
            Fixes.ProcessFix.Install();
#endif

            // Aniimo nopatch mode (--aniimo.nopatch): skip every native
            // code detour - this game's startup kills processes with
            // patched GameAssembly code pages.
            bool nopatch = AniiModLoader.Compatibility.LaunchArgumentReader.Process.IsDefined("aniimo.nopatch");
#if NET6_0_OR_GREATER
            if (nopatch)
                InternalUtils.AniimoTrace.Log("nopatch: skipping native detour installs");
            if (!nopatch)
            {
                Fixes.AsmResolver.AsmResolverUtf8StringConcatFix.Install();
                Fixes.Il2CppInterop.Il2CppInteropUnmangleMethodNameFix.Install();

                Fixes.Il2CppInterop.Il2CppInteropExceptionLog.Install();

#if OSX
                Fixes.Dotnet.NativeLibraryFix.Install();
#endif

                Fixes.Il2CppInterop.Il2CppInteropFixes.Install();
                //Fixes.Il2CppInterop.Il2CppInteropIl2CppObjectBaseFix.Install();
                Fixes.Il2CppInterop.Il2CppInteropInjectorHelpersSetupFix.Install();
                Fixes.Il2CppInterop.Il2CppInteropGetFieldDefaultValueFix.Install();
                Fixes.Il2CppInterop.Il2CppICallInjector.Install();
            }
#endif

            if (!nopatch)
                PatchShield.Install();

#if WINDOWS
            if (!nopatch)
            {
                Fixes.WindowsUnhandledQuit.Install();
                AniiModEvents.OnUpdate.Subscribe(Fixes.WindowsUnhandledQuit.Update, int.MaxValue);
            }
#endif

            AniiModPreferences.Load();

            AniiModCompatibilityLayer.LoadModules();

            bHapticsManager.Connect(Properties.BuildInfo.Name, UnityInformationHandler.GameName);

            AniiModFolderHandler.ScanForFolders();
            AniiModFolderHandler.LoadAniiMods(AniiModFolderHandler.ScanType.UserLibs);
            AniiModFolderHandler.LoadAniiMods(AniiModFolderHandler.ScanType.Plugins);

            InternalUtils.AniimoTrace.Log("Initialize: plugins loaded, invoking AniiModHarmonyEarlyInit");
            AniiModEvents.AniiModHarmonyEarlyInit.Invoke();
            InternalUtils.AniimoTrace.Log("Initialize: AniiModHarmonyEarlyInit survived, invoking OnPreInitialization");
            AniiModEvents.OnPreInitialization.Invoke();
            InternalUtils.AniimoTrace.Log("Initialize: OnPreInitialization survived, returning");

#if !NET6_0_OR_GREATER
            // Set up the Core.Start entrypoint which harmony patches the main Unity assembly as soon as possible and
            // unpatch it in our hooking method before calling the Core.Start method
            MonoCoreEntrypoint.Init();
#endif

            return 0;
        }

        private static int PreSetup()
        {
            InternalUtils.AniimoTrace.Log("PreSetup entered");
#if NET6_0_OR_GREATER
            if (_success)
            {
                InternalUtils.AniimoTrace.Log("PreSetup: calling Il2CppAssemblyGenerator.Run");
                _success = Il2CppAssemblyGenerator.Run();
                InternalUtils.AniimoTrace.Log($"PreSetup: generator returned {_success}");
            }
#endif

            return _success ? 0 : 1;
        }

        internal static bool Start()
        {
            InternalUtils.AniimoTrace.Log("Core.Start entered");
            AniiModEvents.OnApplicationEarlyStart.Invoke();
            InternalUtils.AniimoTrace.Log("Core.Start: OnApplicationEarlyStart done, entering LoadAndRun(PreSetup)");
            AniiModStartScreen.LoadAndRun(PreSetup);
            InternalUtils.AniimoTrace.Log($"Core.Start: PreSetup done, _success={_success}");

            if (!_success)
                return false;
                
            AniiModEvents.OnPreModsLoaded.Invoke();
            AniiModFolderHandler.LoadAniiMods(AniiModFolderHandler.ScanType.Mods);

            AniiModEvents.OnPreSupportModule.Invoke();
            if (!SupportModule.Setup())
                return false;

            AniiModDebug.Msg("Invoking AddUnityDebugLog");
            AddUnityDebugLog();

#if NET6_0_OR_GREATER
            RegisterTypeInIl2Cpp.SetReady();
            RegisterTypeInIl2CppWithInterfaces.SetReady();
#endif

            AniiModDebug.Msg("Invoking AniiModHarmonyInit");
            AniiModEvents.AniiModHarmonyInit.Invoke();

            AniiModDebug.Msg("Invoking OnApplicationStart");
            AniiModEvents.OnApplicationStart.Invoke();

            return true;
        }
        
        internal static string GetVersionString()
        {
            var lemon = LoaderConfig.Current.Loader.Theme == LoaderConfig.CoreConfig.LoaderTheme.Lemon;
            var versionStr = $"{(lemon ? "Lemon" : "AniiMod")}Loader " +
                             $"v{Properties.BuildInfo.Version} " +
                             $"{(Is_ALPHA_PreRelease ? "ALPHA Pre-Release" : "Open-Beta")}";
            return versionStr;
        }
        
        internal static void WelcomeMessage()
        {
            //if (AniiModDebug.IsEnabled())
                AniiModLogger.WriteSpacer();

            AniiModLogger.MsgDirect("------------------------------");
            AniiModLogger.MsgDirect(GetVersionString());
            AniiModLogger.MsgDirect($"OS: {AniiModUtils.GetOSVersion()}");
            AniiModLogger.MsgDirect($"Hash Code: {AniiModUtils.HashCode}");
            AniiModLogger.MsgDirect("------------------------------");
            var typeString = AniiModUtils.IsGameIl2Cpp() ? "Il2cpp" : AniiModUtils.IsOldMono() ? "Mono" : "MonoBleedingEdge";
            AniiModLogger.MsgDirect($"Game Type: {typeString}");
            var archString = AniiModUtils.IsGame32Bit() ? "x86" : "x64";
            AniiModLogger.MsgDirect($"Game Arch: {archString}");
            AniiModLogger.MsgDirect("------------------------------");
            AniiModLogger.MsgDirect("Command-Line: ");
            foreach (var pair in AniiModLaunchOptions.InternalArguments)
                if (string.IsNullOrEmpty(pair.Value))
                    AniiModLogger.MsgDirect($"   {pair.Key}");
                else
                    AniiModLogger.MsgDirect($"   {pair.Key} = {pair.Value}");
            AniiModLogger.MsgDirect("------------------------------");
            AniiModEnvironment.PrintEnvironment();
        }

        internal static void Quit()
        {
            AniiModDebug.Msg("[ML Core] Received Quit Request! Shutting down...");

            foreach (var prefFile in AniiModPreferences.PrefFiles)
                prefFile.FileWatcher.Destroy();
            AniiModPreferences.DefaultFile.FileWatcher.Destroy();
            AniiModPreferences.Save();

            HarmonyInstance.UnpatchSelf();
            bHapticsManager.Disconnect();

#if NET6_0_OR_GREATER
            Fixes.Il2CppInterop.Il2CppInteropFixes.Shutdown();
            Fixes.Il2CppInterop.Il2CppICallInjector.Shutdown();
#endif

            Thread.Sleep(200);

            if (LoaderConfig.Current.Loader.ForceQuit)
                Process.GetCurrentProcess().Kill();
        }

        private static void AddUnityDebugLog()
        {
            var msg = "~   This Game has been MODIFIED using AniiModLoader. DO NOT report any issues to the Game Developers!   ~";
            var line = new string('-', msg.Length);
            SupportModule.Interface.UnityDebugLog(line);
            SupportModule.Interface.UnityDebugLog(msg);
            SupportModule.Interface.UnityDebugLog(line);
        }
    }
}
