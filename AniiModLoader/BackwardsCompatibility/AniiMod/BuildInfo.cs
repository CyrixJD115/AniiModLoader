using Semver;
using System;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.BuildInfo is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo instead. This will be removed in a future update.", true)]
    public static class BuildInfo
    {
        [Obsolete("AniiModLoader.BuildInfo.Name is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.Name instead. This will be removed in a future update.", true)]
        public static string Name { get => Properties.BuildInfo.Name; }
        [Obsolete("AniiModLoader.BuildInfo.Description is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.Description instead. This will be removed in a future update.", true)]
        public static string Description { get => Properties.BuildInfo.Description; }
        [Obsolete("AniiModLoader.BuildInfo.Author is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.Author instead. This will be removed in a future update.", true)]
        public static string Author { get => Properties.BuildInfo.Author; }
        [Obsolete("AniiModLoader.BuildInfo.Company is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.Company instead. This will be removed in a future update.", true)]
        public static string Company { get => Properties.BuildInfo.Company; }
        [Obsolete("AniiModLoader.BuildInfo.Version is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.Version instead. This will be removed in a future update.", true)]
        public static string Version { get => Properties.BuildInfo.Version; }
        [Obsolete("AniiModLoader.BuildInfo.VersionNumber is Only Here for Compatibility Reasons. Please use AniiModLoader.Properties.BuildInfo.VersionNumber instead. This will be removed in a future update.", true)]
        public static SemVersion VersionNumber { get => Properties.BuildInfo.VersionNumber; }
    }
}