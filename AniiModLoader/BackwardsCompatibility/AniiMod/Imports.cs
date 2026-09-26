using System;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.Imports is Only Here for Compatibility Reasons. This will be removed in a future update.", true)]
    public static class Imports
    {
        [Obsolete("AniiModLoader.Imports.GetCompanyName is Only Here for Compatibility Reasons. Please use AniiModLoader.InternalUtils.UnityInformationHandler.GameDeveloper instead. This will be removed in a future update.", true)]
        public static string GetCompanyName() => InternalUtils.UnityInformationHandler.GameDeveloper;
        [Obsolete("AniiModLoader.Imports.GetProductName is Only Here for Compatibility Reasons. Please use AniiModLoader.InternalUtils.UnityInformationHandler.GameName instead. This will be removed in a future update.", true)]
        public static string GetProductName() => InternalUtils.UnityInformationHandler.GameName;
        [Obsolete("AniiModLoader.Imports.GetGameDirectory is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.AniiModEnvironment.GameRootDirectory instead. This will be removed in a future update.", true)]
        public static string GetGameDirectory() => AniiModEnvironment.GameRootDirectory;
        [Obsolete("AniiModLoader.Imports.GetGameDataDirectory is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.AniiModEnvironment.UnityGameDataDirectory instead. This will be removed in a future update.", true)]
        public static string GetGameDataDirectory() => AniiModEnvironment.UnityGameDataDirectory;
        [Obsolete("AniiModLoader.Imports.GetAssemblyDirectory is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.AniiModEnvironment.AniiModManagedDirectory instead. This will be removed in a future update.", true)]
        public static string GetAssemblyDirectory() => AniiModEnvironment.AniiModManagedDirectory;
        [Obsolete("AniiModLoader.Imports.IsIl2CppGame is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModUtils.IsGameIl2Cpp instead. This will be removed in a future update.", true)]
        public static bool IsIl2CppGame() => AniiModUtils.IsGameIl2Cpp();
        [Obsolete("AniiModLoader.Imports.IsDebugMode is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModDebug.IsEnabled instead. This will be removed in a future update.", true)]
        public static bool IsDebugMode() => AniiModDebug.IsEnabled();
        [Obsolete("AniiModLoader.Imports.Hook is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModUtils.NativeHookAttach instead. This will be removed in a future update.", true)]
        public static void Hook(IntPtr target, IntPtr detour) => AniiModUtils.NativeHookAttach(target, detour);
        [Obsolete("AniiModLoader.Imports.Unhook is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModUtils.NativeHookDetach instead. This will be removed in a future update.", true)]
        public static void Unhook(IntPtr target, IntPtr detour) => AniiModUtils.NativeHookDetach(target, detour);
    }
}