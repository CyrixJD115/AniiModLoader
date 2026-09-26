using System;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModPluginGameAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame instead. This will be removed in a future update.", true)]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AniiModPluginGameAttribute : AniiModGameAttribute
    {
        [Obsolete("AniiModLoader.AniiModPluginGameAttribute.Developer is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame.Developer instead. This will be removed in a future update.", true)]
        new public string Developer => base.Developer;
        [Obsolete("AniiModLoader.AniiModPluginGameAttribute.GameName is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame.Name instead. This will be removed in a future update.", true)]
        public string GameName => Name;
        [Obsolete("AniiModLoader.AniiModPluginGameAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame instead. This will be removed in a future update.", true)]
        public AniiModPluginGameAttribute(string developer = null, string gameName = null) : base(developer, gameName) { }
    }
}