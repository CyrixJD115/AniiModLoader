#if NET6_0_OR_GREATER

using AniiModLoader.Modules;
using System;
using System.Diagnostics;
using System.IO;
using AniiModLoader.Utils;

namespace AniiModLoader.InternalUtils
{
    internal static class Il2CppAssemblyGenerator
    {
        private static readonly string modulePath = Path.Combine(AniiModEnvironment.Il2CppAssemblyGeneratorDirectory, "Il2CppAssemblyGenerator.dll");
        public static readonly AniiModModule.Info moduleInfo = new AniiModModule.Info(modulePath, () => !AniiModUtils.IsGameIl2Cpp());

        internal static bool Run()
        {
            if (AniiModEnvironment.IsMonoRuntime)
                return true;

            AniiModLogger.MsgDirect("Loading Il2CppAssemblyGenerator...");

            // Aniimo: generation failure must disable mods, never kill the
            // game. Under Wine/Proton the console-window calls threw
            // Win32Exception (Invalid window handle) straight into native.
            try
            {
                var module = AniiModModule.Load(moduleInfo);
                if (module == null)
                {
                    if (File.Exists(modulePath))
                        AniiModLogger.Error("Failed to Load Il2CppAssemblyGenerator!");
                    else
                        AniiModLogger.Error("Il2CppAssemblyGenerator was Not Found!");
                    return false;
                }

                if (AniiModUtils.IsWindows)
                {
                    try
                    {
                        IntPtr windowHandle = Process.GetCurrentProcess().MainWindowHandle;
                        if (windowHandle != IntPtr.Zero)
                        {
#if WINDOWS
                            BootstrapInterop.DisableCloseButton(windowHandle);
#endif
                        }
                    }
                    catch { /* no window under Wine; cosmetic call */ }
                }

                var ret = module.SendMessage("Run");

                if (AniiModUtils.IsWindows)
                {
                    try
                    {
                        IntPtr windowHandle = Process.GetCurrentProcess().MainWindowHandle;
                        if (windowHandle != IntPtr.Zero)
                        {
#if WINDOWS
                            BootstrapInterop.EnableCloseButton(windowHandle);
#endif
                        }
                    }
                    catch { /* no window under Wine; cosmetic call */ }
                }

                return ret is 0;
            }
            catch (Exception error)
            {
                AniiModLogger.Error("Il2CppAssemblyGenerator failed:");
                AniiModLogger.Error(error);
                return false;
            }
        }
    }
}

#endif