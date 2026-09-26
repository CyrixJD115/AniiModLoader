using Semver;

namespace AniiModLoader.Properties
{
    public static class BuildInfo
    {
        public const string Name = "AniiModLoader";
        public const string Description = "An Aniimo-focused Unity IL2CPP mod loader.";
        public const string Author = "AniiModLoader contributors";
        public const string Company = "AniiModLoader contributors";

        public static string Version { get; private set; }
        public static SemVersion VersionNumber { get; private set; }

        static BuildInfo()
        {
            var version = typeof(BuildInfo).Assembly.GetName().Version!;
            VersionNumber = new(version.Major, version.Minor, version.Build, ((version.Revision == 0)
                ? ""
                : ("ci." + version.Revision.ToString())));
            Version = VersionNumber.ToString();
        }
    }
}