using System;
using System.Linq;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModPlatformAttribute : Attribute
    {
        public AniiModPlatformAttribute(params CompatiblePlatforms[] platforms) => Platforms = platforms;

        // <summary>Enum for AniiMod Platform Compatibility.</summary>
        public enum CompatiblePlatforms
        {
            UNIVERSAL,
            WINDOWS_X86,
            WINDOWS_X64,
            ANDROID,
            LINUX,
            MAC,
        };

        // <summary>Platforms Compatible with the AniiMod.</summary>
        public CompatiblePlatforms[] Platforms { get; internal set; }

        public bool IsCompatible(CompatiblePlatforms platform)
            => Platforms == null || Platforms.Length == 0 || Platforms.Contains(platform);
    }
}