using System;
using System.Collections.Generic;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.Main is Only Here for Compatibility Reasons. This will be removed in a future update.", true)]
    public static class Main
    {
        [Obsolete("AniiModLoader.Main.Mods is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModHandler.Mods instead. This will be removed in a future update.", true)]
        public static List<AniiModMod> Mods = null;
        [Obsolete("AniiModLoader.Main.Plugins is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModHandler.Plugins instead. This will be removed in a future update.", true)]
        public static List<AniiModPlugin> Plugins = null;
        [Obsolete("AniiModLoader.Main.IsBoneworks is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModUtils.IsBONEWORKS instead. This will be removed in a future update.", true)]
        public static bool IsBoneworks = false;
        [Obsolete("AniiModLoader.Main.GetUnityVersion is Only Here for Compatibility Reasons. Please use  AniiModLoader.InternalUtils.UnityInformationHandler.EngineVersion instead. This will be removed in a future update.", true)]
        public static string GetUnityVersion() => InternalUtils.UnityInformationHandler.EngineVersion.ToStringWithoutType();
        [Obsolete("AniiModLoader.Main.GetUserDataPath is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.AniiModEnvironment.UserDataDirectory instead. This will be removed in a future update.", true)]
        public static string GetUserDataPath() => AniiModEnvironment.UserDataDirectory;
    }
}