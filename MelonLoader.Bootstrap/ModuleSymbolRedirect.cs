using MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using MelonLoader.Bootstrap.RuntimeHandlers.Mono;
using MelonLoader.Bootstrap.Utils;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap
{
    internal static partial class ModuleSymbolRedirect
    {
        // Aniimo: Unity 2022.3.62+ obfuscates GameAssembly export names,
        // so stock name-based triggers never fire. Il2CppNameMap.txt
        // (real=scrambled) restores name-based operation; --aniimo.safe
        // and --aniimo.earlystart select observation / no-invoke-detour
        // modes for this game.
        private static Dictionary<string, string>? _realToScrambled;
        private static Dictionary<string, string>? _scrambledToReal;

        internal static bool SafeMode { get; private set; }
        internal static bool EarlyStart { get; private set; }

        internal static void LoadNameMap()
        {
            try
            {
                var path = Path.Combine(
                    LoaderConfig.Current.Loader.BaseDirectory, "MelonLoader", "Il2CppNameMap.txt");
                if (!File.Exists(path))
                {
                    Trace.Log("Il2CppNameMap.txt not present - name translation off");
                    return;
                }
                _realToScrambled = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(path))
                {
                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;
                    var real = line[..idx].Trim();
                    var scr = line[(idx + 1)..].Trim();
                    if (real.Length == 0 || scr.Length == 0)
                        continue;
                    _realToScrambled[real] = scr;
                }
                _scrambledToReal = new Dictionary<string, string>();
                foreach (var pair in _realToScrambled)
                    _scrambledToReal[pair.Value] = pair.Key;
                Trace.Log($"Il2CppNameMap loaded: {_realToScrambled.Count} entries");
            }
            catch (Exception error)
            {
                Trace.Log($"Il2CppNameMap load failed: {error.Message}");
            }
        }

        private static string TranslateRequest(string symbolName)
            => _scrambledToReal is not null
               && _scrambledToReal.TryGetValue(symbolName, out var real)
                ? real
                : symbolName;

        private static string TranslateResolve(string symbolName)
            => _realToScrambled is not null
               && _realToScrambled.TryGetValue(symbolName, out var scr)
                ? scr
                : symbolName;

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
            MelonDebug.Log("Attaching Symbol Redirect...");

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

            MelonDebug.Log("Symbol Redirect Attached!");
        }

        internal static nint GetSymbol(nint handle, string symbolName)
            => GetSymbol(handle, Marshal.StringToHGlobalAnsi(TranslateResolve(symbolName)));
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

            //MelonDebug.Log($"Looking for Symbol {symbolName}");
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
                MelonDebug.Log("Initializing Runtime");
                redirect.InitMethod(handle);
                if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
                    ConsoleHandler.ResetHandles();
            }

            MelonDebug.Log($"Redirecting {symbolName}");
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