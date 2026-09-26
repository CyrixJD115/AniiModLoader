using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModPlatformDomainAttribute : Attribute
    {
        public AniiModPlatformDomainAttribute(CompatibleDomains domain = CompatibleDomains.UNIVERSAL) => Domain = domain;

        // <summary>Enum for AniiMod Platform Domain Compatibility.</summary>
        public enum CompatibleDomains
        {
            UNIVERSAL,
            MONO,
            IL2CPP
        };

        // <summary>Platform Domain Compatibility of the AniiMod.</summary>
        public CompatibleDomains Domain { get; internal set; }

        public bool IsCompatible(CompatibleDomains domain)
            => Domain == CompatibleDomains.UNIVERSAL || domain == CompatibleDomains.UNIVERSAL || Domain == domain;
    }
}