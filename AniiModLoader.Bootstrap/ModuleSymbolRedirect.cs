using AniiModLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using AniiModLoader.Bootstrap.RuntimeHandlers.Mono;
using AniiModLoader.Bootstrap.Utils;
using System.Runtime.InteropServices;

namespace AniiModLoader.Bootstrap
{
    internal static partial class ModuleSymbolRedirect
    {
        // Aniimo: Unity 2022.3.62+ obfuscates GameAssembly export names,
        // so stock name-based triggers never fire. Il2CppNameMap.txt
        // (real=scrambled) restores name-based operation; --aniimo.safe
        // and --aniimo.earlystart select observation / no-invoke-detour
        // modes for this game.
        private static Compatibility.Il2CppExportNameMap? _nameMap;
        private static readonly object NameMapGate = new();
        private static bool _nameMapLoaded;
        internal static bool SafeMode { get; private set; }
        internal static bool EarlyStart { get; private set; }

        internal static void LoadNameMap()
        {
            lock (NameMapGate)
            {
                if (_nameMapLoaded) return;
                _nameMapLoaded = true;
                try
                {
                    var path = Compatibility.LoaderPaths.ExistingDataPath(LoaderConfig.Current.Loader.BaseDirectory, "Il2CppNameMap.txt");
                    _nameMap = new Compatibility.Il2CppExportNameMap(path);
                    Trace.Log($"Il2CppNameMap loaded: {_nameMap.Count} entries");
                }
                catch (Exception error) { Trace.Log($"Il2CppNameMap load failed: {error.Message}"); }
            }
        }
        private static string TranslateRequest(string symbolName) => _nameMap?.Original(symbolName) ?? symbolName;
        private static string TranslateResolve(string symbolName) => _nameMap?.Resolve(symbolName) ?? symbolName;

        private static bool _runtimeInitialised;

#if LINUX || OSX
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#endif
#if WINDOWS
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
#endif
        private delegate nint DetourFn(nint handle, nint symbol);
        private static readonly DetourFn DetourDelegate = SymbolDetour;

        internal static void Attach()
        {
            AniiModDebug.Log("Attaching Symbol Redirect...");

            SafeMode = Utils.ArgParser.IsDefined("aniimo.safe");
            if (SafeMode)
                Trace.Log("SAFE MODE (--aniimo.safe): log-only observation, no redirects");
            EarlyStart = Utils.ArgParser.IsDefined("aniimo.earlystart");
            if (EarlyStart)
                Trace.Log("EARLY START (--aniimo.earlystart): invoke detour off, start from init");

            LoadNameMap();

            IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(DetourDelegate);

#if LINUX || OSX
            PltHook.InstallHooks
            ([
                ("dlsym", detourPtr)
            ]);
#endif
#if WINDOWS
            PltHook.InstallHooks
            ([
                ("GetProcAddress", detourPtr)
            ]);
#endif

            AniiModDebug.Log("Symbol Redirect Attached!");
        }

        internal static nint GetSymbol(nint handle, string symbolName)
        {
            var name = Marshal.StringToHGlobalAnsi(TranslateResolve(symbolName));
            try { return GetSymbol(handle, name); }
            finally { Marshal.FreeHGlobal(name); }
        }
        private static nint GetSymbol(nint handle, nint symbolName)
        {
            if ((handle == nint.Zero)
                || (symbolName == nint.Zero))
                return nint.Zero;

#if WINDOWS
            return GetProcAddress(handle, symbolName);
#elif LINUX || OSX
            return dlsym(handle, symbolName);
#else
            return nint.Zero;
#endif
        }

        private static nint SymbolDetour(nint handle, nint symbol)
        {
            nint originalSymbolAddress = GetSymbol(handle, symbol);

            string? symbolName = Marshal.PtrToStringAnsi(symbol);
            if (string.IsNullOrEmpty(symbolName)
                || string.IsNullOrWhiteSpace(symbolName))
                return originalSymbolAddress;

            //AniiModDebug.Log($"Looking for Symbol {symbolName}");
            if (SafeMode)
                return originalSymbolAddress;

            var translated = TranslateRequest(symbolName);

            if (EarlyStart && translated == "il2cpp_runtime_invoke")
            {
                Trace.Log("earlystart: runtime_invoke left un-redirected (original pointer)");
                return originalSymbolAddress;
            }

            if (!MonoHandler.SymbolRedirects.TryGetValue(translated, out var redirect)
                && !Il2CppHandler.SymbolRedirects.TryGetValue(translated, out redirect))
                return originalSymbolAddress;

            Trace.Log($"Symbol redirect MATCH: {symbolName} -> {translated}");

            if (!_runtimeInitialised)
            {
                _runtimeInitialised = true;
                AniiModDebug.Log("Initializing Runtime");
                redirect.InitMethod(handle);
                if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
                    ConsoleHandler.ResetHandles();
            }

            AniiModDebug.Log($"Redirecting {symbolName}");
            return redirect.detourPtr;
        }

#if WINDOWS
        [DllImport("kernel32")]
        private static extern nint GetProcAddress(nint handle, nint symbol);
#elif LINUX
        [DllImport("libdl.so.2")]
        private static extern IntPtr dlsym(nint handle, nint symbol);
#elif OSX
        [DllImport("libSystem.B.dylib")]
        private static extern IntPtr dlsym(nint handle, nint symbol);
#endif
    }
}