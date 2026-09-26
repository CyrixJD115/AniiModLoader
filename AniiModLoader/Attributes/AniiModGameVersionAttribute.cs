using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AniiModGameVersionAttribute : Attribute
    {
        public AniiModGameVersionAttribute(string version = null)
            => Version = version;

        /// <summary>
        /// Version of the Game.
        /// </summary>
        public string Version { get; internal set; }

        /// <summary>
        /// If the Attribute is set as Universal or not.
        /// </summary>
        public bool Universal { get => string.IsNullOrEmpty(Version); }
    }
}