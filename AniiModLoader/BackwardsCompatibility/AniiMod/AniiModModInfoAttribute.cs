using System;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModPluginInfoAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo instead. This will be removed in a future update.", true)]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class AniiModModInfoAttribute : AniiModInfoAttribute
    {
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute.SystemType is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo.SystemType instead. This will be removed in a future update.", true)]
        new public Type SystemType => base.SystemType;
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute.Name is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo.Name instead. This will be removed in a future update.", true)]
        new public string Name => base.Name;
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute.Version is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo.Version instead. This will be removed in a future update.", true)]
        new public string Version => base.Version;
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute.Author is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo.Author instead. This will be removed in a future update.", true)]
        new public string Author => base.Author;
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute.DownloadLink is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo.DownloadLink instead. This will be removed in a future update.", true)]
        new public string DownloadLink => base.DownloadLink;
        [Obsolete("AniiModLoader.AniiModPluginInfoAttribute is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModInfo instead. This will be removed in a future update.", true)]
        public AniiModModInfoAttribute(Type type, string name, string version, string author, string downloadLink = null) : base(type, name, version, author, downloadLink) { }
    }
}