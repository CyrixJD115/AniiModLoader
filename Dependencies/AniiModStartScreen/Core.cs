using AniiModLoader.AniiModStartScreen.NativeUtils;
using AniiModLoader.Modules;
using AniiModLoader.NativeUtils.PEParser;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows;
using AniiModLoader.Utils;
using AniiModLoader.NativeUtils;

namespace AniiModLoader.AniiModStartScreen
{
    internal class Core : AniiModModule
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr User32SetTimerDelegate(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);
        private static NativeHook<User32SetTimerDelegate> user32Hook = new();

        private static bool nextSetTimerIsUnity = false;
        private static IntPtr titleBarTimer;

        private static bool functionRunDone = false;
        private static int functionRunResult = 0;

        internal static string FolderPath;
        internal static string ThemesFolderPath;

        public static Core instance;

        public static AniiModLogger.Instance Logger => instance.LoggerInstance;

        public override void OnInitialize() 
        {
            instance = this;
        }

        private static int LoadAndRun(LemonFunc<int> functionToWaitForAsync)
        {
            // Start Screen has no signatures for Development Builds of UnityPlayer.dll
            if (AniiModUnityEngine.UnityDebug.isDebugBuild)
                return functionToWaitForAsync();

            Logger.Msg("Initializing...");

            FolderPath = Path.Combine(AniiModEnvironment.UserDataDirectory, "AniiModStartScreen");
            string legacyFolder = Path.Combine(AniiModEnvironment.UserDataDirectory, "MelonStartScreen");
            if (!Directory.Exists(FolderPath) && Directory.Exists(legacyFolder))
                FolderPath = legacyFolder;
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            ThemesFolderPath = Path.Combine(FolderPath, "Themes");
            if (!Directory.Exists(ThemesFolderPath))
                Directory.CreateDirectory(ThemesFolderPath);

            UI_Theme.Load();
            if (!UI_Theme.General.Enabled)
                return functionToWaitForAsync();

            // We try to resolve all the signatures, which are available for Unity 2018.1.0+
            // If we can't find them (signatures changed or <2018.1.0), then we run the function and return.
            try
            {
                if (!NativeSignatureResolver.Apply())
                    return functionToWaitForAsync();

                if (!ApplyUser32SetTimerPatch())
                    return functionToWaitForAsync();

                AniiModDebug.Msg("Initializing Screen Renderer");
                ScreenRenderer.Init();
                AniiModDebug.Msg("Screen Renderer initialized");

                RegisterMessageCallbacks();

                // Initial render
                ScreenRenderer.Render();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                ScreenRenderer.disabled = true;
                return functionToWaitForAsync();
            }

            SubscribeToCoreCallbacks();

            StartFunction(functionToWaitForAsync);
            MainLoop();

            return functionRunResult;
        }

        private static void SubscribeToCoreCallbacks()
        {
            AniiModEvents.OnApplicationLateStart.Subscribe(Finish, int.MinValue);
            AniiModEvents.OnApplicationStart.Subscribe(OnApplicationStart, int.MaxValue);
            AniiModBase.OnAniiModInitializing.Subscribe(OnAniiModInitializing, 100);
            AniiModAssembly.OnAssemblyResolving.Subscribe(OnAniiModsResolving, 100);
        }

        private static void RegisterMessageCallbacks()
        {
            AniiModLogger.MsgDrawingCallbackHandler += (namesection_color, txt_color, namesection, txt) => ScreenRenderer.UpdateProgressFromLog(txt);
            AniiModDebug.MsgCallbackHandler += (txt_color, txt) => ScreenRenderer.UpdateProgressFromLog(txt);
        }

        #region User32::SetTime Path

        private static unsafe bool ApplyUser32SetTimerPatch()
        {
            IntPtr original = PEUtils.GetExportedFunctionPointerForModule("USER32.dll", "SetTimer");
            AniiModDebug.Msg($"User32::SetTimer original: 0x{(long)original:X}");

            if (original == IntPtr.Zero)
            {
                AniiModDebug.Error("Failed to find USER32.dll::SetTimer");
                return false;
            }

            // We get a native function pointer to User32SetTimerDetour from our current class
#if NET6_0_OR_GREATER
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, IntPtr, IntPtr> detourPtr = &User32SetTimerDetour;
#else
            IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate((User32SetTimerDelegate)User32SetTimerDetour);

            if (detourPtr == IntPtr.Zero)
            {
                AniiModDebug.Error("Failed to find User32SetTimerDetour");
                return false;
            }
#endif
            user32Hook.Target = original;
            user32Hook.Detour = (IntPtr)detourPtr;

            // And we patch SetTimer to replace it by our hook
            AniiModDebug.Msg($"Applying USER32.dll::SetTimer Hook at 0x{original.ToInt64():X}");
            user32Hook.Attach();
            AniiModDebug.Msg("Applied USER32.dll::SetTimer patch");

            return true;
        }

#if NET6_0_OR_GREATER
        [UnmanagedCallersOnly(CallConvs = new[] {typeof(CallConvCdecl)})]
#endif
        private unsafe static IntPtr User32SetTimerDetour(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr timerProc)
        {
            if (nextSetTimerIsUnity)
            {
                nextSetTimerIsUnity = false;
                return IntPtr.Zero;
            }

            return user32Hook.Trampoline(hWnd, nIDEvent, uElapse, timerProc);
        }

        #endregion

        private static void StartFunction(LemonFunc<int> func)
        {
            new Thread(() =>
            {
                functionRunResult = func();
                functionRunDone = true;
            })
            {
                IsBackground = true,
                Name = "AniiModStartScreen Function Thread"
            }.Start();
        }

        private static void MainLoop()
        {
            while (!functionRunDone) // WM_QUIT
            {
                ProcessEventsAndRender(true);
            }

            if (titleBarTimer != IntPtr.Zero)
            {
                User32.KillTimer(IntPtr.Zero, titleBarTimer);
                titleBarTimer = IntPtr.Zero;
            }
        }

        #region Event Processing and Rendering

        private static void ProcessEventsAndRender(bool isMainLoop = false)
        {
            ProcessMessages();

            if (titleBarTimer != IntPtr.Zero)
            {
                User32.KillTimer(IntPtr.Zero, titleBarTimer);
                titleBarTimer = IntPtr.Zero;
            }
            ScreenRenderer.Render();
            if (isMainLoop)
                Thread.Sleep(16); // ~60fps
            else
            {
                if (titleBarTimer != IntPtr.Zero)
                {
                    User32.KillTimer(IntPtr.Zero, titleBarTimer);
                    titleBarTimer = IntPtr.Zero;
                }
            }
        }

        private static void ProcessMessages()
        {
            while (true)
            {
                User32.PeekMessage(out Msg msg, IntPtr.Zero, 0, 0, 0);
                if (msg.message == WindowMessage.QUIT)
                {
                    Process.GetCurrentProcess().Kill();
                    return;
                }
                else if (User32.PeekMessage(out msg, IntPtr.Zero, 0, 0, 1)) // If there a message pending
                {
                    if (msg.message == WindowMessage.NCLBUTTONDOWN || msg.message == (WindowMessage)0x242 /* NCPOINTERDOWN */)
                    {
                        if (titleBarTimer == IntPtr.Zero)
                            titleBarTimer = User32.SetTimer(IntPtr.Zero, IntPtr.Zero, 10, TitleBarTimerUpdateCallback);
                        nextSetTimerIsUnity = true;
                    }
                    else if (msg.message == WindowMessage.PAINT)
                        ScreenRenderer.Render();

                    User32.TranslateMessage(ref msg);
                    User32.DispatchMessage(ref msg);
                }
                else
                    return;
            }
        }

        private static void TitleBarTimerUpdateCallback(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime)
        {
            ScreenRenderer.Render();
        }

        #endregion

        #region Calls from AniiModLoader

        internal static void OnAniiModInitializing(AniiModBase aniimod)
        {
            ScreenRenderer.UpdateProgressState(ModLoadStep.InitializeAniiMods);
            ScreenRenderer.UpdateProgressFromMod(aniimod);
            ProcessEventsAndRender();
        }

        internal static void OnAniiModsResolving(Assembly asm)
        {
            ScreenRenderer.UpdateProgressState(ModLoadStep.LoadAniiMods);
            ScreenRenderer.UpdateProgressFromModAssembly(asm);
            ProcessEventsAndRender();
        }

        internal static void OnApplicationStart()
        {
            ScreenRenderer.UpdateProgressState(ModLoadStep.OnApplicationStart);
            ProcessEventsAndRender();
        }

        internal static void DisplayModLoadIssuesIfNeeded()
        {
            // TODO Start a new locking thread with a display of the issues and buttons to either close the game or continue
        }

        internal static void Finish()
        {
            ScreenRenderer.UpdateMainProgress("Starting game...", 1f);
            ScreenRenderer.Render(); // Final render, to set the progress bar to 100%

            AniiModEvents.OnApplicationLateStart.Unsubscribe(typeof(Core).GetMethod(nameof(Finish), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            AniiModEvents.OnApplicationStart.Unsubscribe(typeof(Core).GetMethod(nameof(OnApplicationStart), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            AniiModBase.OnAniiModInitializing.Unsubscribe(typeof(Core).GetMethod(nameof(OnAniiModInitializing), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            AniiModAssembly.OnAssemblyResolving.Unsubscribe(typeof(Core).GetMethod(nameof(OnAniiModsResolving), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        }

        #endregion
    }
}
