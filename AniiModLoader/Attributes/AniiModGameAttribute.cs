using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AniiModGameAttribute : Attribute
    {
        public AniiModGameAttribute(string developer = null, string name = null) { Developer = developer; Name = name; }

        /// <summary>
        /// Developer of the Game.
        /// </summary>
        public string Developer { get; internal set; }

        /// <summary>
        /// Name of the Game.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// If the Attribute is set as Universal or not.
        /// </summary>
        public bool Universal { get => (string.IsNullOrEmpty(Developer) || Developer.Equals("UNKNOWN") || string.IsNullOrEmpty(Name) || Name.Equals("UNKNOWN")); }

        /// <summary>
        /// Returns true or false if the Game is compatible with this Assembly.
        /// </summary>
        public bool IsCompatible(string developer, string gameName) => (Universal || (!string.IsNullOrEmpty(developer) && Developer.Equals(developer) && !string.IsNullOrEmpty(gameName) && Name.Equals(gameName)));
        
        /// <summary>
        /// Returns true or false if the Game is compatible with this Assembly.
        /// </summary>
        public bool IsCompatible(AniiModGameAttribute att) => (IsCompatibleBecauseUniversal(att) || (att.Developer.Equals(Developer) && att.Name.Equals(Name)));
        
        /// <summary>
        /// Returns true or false if the Game is compatible with this Assembly specifically because of Universal Compatibility.
        /// </summary>
        public bool IsCompatibleBecauseUniversal(AniiModGameAttribute att) => ((att == null) || Universal || att.Universal);

        [Obsolete("IsCompatible(AniiModModGameAttribute) is obsolete. Please use IsCompatible(AniiModGameAttribute) instead. This will be removed in a future update.", true)]
        public bool IsCompatible(AniiModModGameAttribute att) => ((att == null) || IsCompatibleBecauseUniversal(att) || (att.Developer.Equals(Developer) && att.GameName.Equals(Name)));
        [Obsolete("IsCompatible(AniiModPluginGameAttribute) is obsolete. Please use IsCompatible(AniiModGameAttribute) instead. This will be removed in a future update.", true)]
        public bool IsCompatible(AniiModPluginGameAttribute att) => ((att == null) || IsCompatibleBecauseUniversal(att) || (att.Developer.Equals(Developer) && att.GameName.Equals(Name)));
        [Obsolete("IsCompatibleBecauseUniversal(AniiModModGameAttribute) is obsolete. Please use IsCompatible(AniiModGameAttribute) instead. This will be removed in a future update.", true)]
        public bool IsCompatibleBecauseUniversal(AniiModModGameAttribute att) => ((att == null) || Universal || (string.IsNullOrEmpty(att.Developer) || string.IsNullOrEmpty(att.GameName)));
        [Obsolete("IsCompatibleBecauseUniversal(AniiModPluginGameAttribute) is obsolete. Please use IsCompatible(AniiModGameAttribute) instead. This will be removed in a future update.", true)]
        public bool IsCompatibleBecauseUniversal(AniiModPluginGameAttribute att) => ((att == null) || Universal || (string.IsNullOrEmpty(att.Developer) || string.IsNullOrEmpty(att.GameName)));
    }
}