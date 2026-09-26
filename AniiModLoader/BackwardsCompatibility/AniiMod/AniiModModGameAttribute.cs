using System;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModModGameAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame instead. This will be removed in a future update.", true)]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AniiModModGameAttribute : AniiModGameAttribute
    {
        [Obsolete("AniiModLoader.AniiModModGameAttribute.Developer is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame.Developer instead. This will be removed in a future update.", true)]
        new public string Developer => base.Developer;
        [Obsolete("AniiModLoader.AniiModModGameAttribute.GameName is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame.Name instead. This will be removed in a future update.", true)]
        public string GameName => Name;
        [Obsolete("AniiModLoader.AniiModModGameAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModGame instead. This will be removed in a future update.", true)]
        public AniiModModGameAttribute(string developer = null, string gameName = null) : base(developer, gameName) { }
    }
}