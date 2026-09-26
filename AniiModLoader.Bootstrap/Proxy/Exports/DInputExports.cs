#if WINDOWS
using System.Runtime.InteropServices;

namespace AniiModLoader.Bootstrap.Proxy.Exports;

internal static class DInputExports
{
    [UnmanagedCallersOnly(EntryPoint = "ImplDirectInputCreateA")]
    public static void ImplDirectInputCreateA() { }
    [UnmanagedCallersOnly(EntryPoint = "ImplDirectInputCreateEx")]
    public static void ImplDirectInputCreateEx() { }
    [UnmanagedCallersOnly(EntryPoint = "ImplDirectInputCreateW")]
    public static void ImplDirectInputCreateW() { }
}
#endif
