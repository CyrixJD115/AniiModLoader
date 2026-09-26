using System;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModLoaderBase is Only Here for Compatibility Reasons. This will be removed in a future update.", true)]
    public static class AniiModLoaderBase
    {
        [Obsolete("AniiModLoader.AniiModLoaderBase.UserDataPath is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.AniiModEnvironment.UserDataDirectory instead. This will be removed in a future update.", true)]
        public static string UserDataPath { get => AniiModEnvironment.UserDataDirectory; }
        [Obsolete("AniiModLoader.AniiModLoaderBase.UnityVersion is Only Here for Compatibility Reasons. Please use AniiModLoader.InternalUtils.UnityInformationHandler.EngineVersion instead. This will be removed in a future update.", true)]
        public static string UnityVersion { get => InternalUtils.UnityInformationHandler.EngineVersion.ToStringWithoutType(); }
    }
}