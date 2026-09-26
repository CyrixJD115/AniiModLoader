using Semver;
using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AniiModInfoAttribute : Attribute
    {
        /// <summary>
        /// System.Type of the AniiMod.
        /// </summary>
        public Type SystemType { get; internal set; }

        /// <summary>
        /// Name of the AniiMod.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Version of the AniiMod.
        /// </summary>
        public string Version { get; internal set; }

        /// <summary>
        /// Semantic Version of the AniiMod. Will be null if Version is not using the <see href="https://semver.org">Semantic Versioning</see> format.
        /// </summary>
        public SemVersion SemanticVersion { get; internal set; }

        /// <summary>
        /// Author of the AniiMod.
        /// </summary>
        public string Author { get; internal set; } // This used to be optional, but is now required

        /// <summary>
        /// Download Link of the AniiMod.
        /// </summary>
        public string DownloadLink { get; internal set; } // Might get Removed. Not sure yet.

        /// <summary>
        /// AniiModInfo constructor.
        /// </summary>
        /// <param name="type">The main AniiMod type of the AniiMod (for example TestMod)</param>
        /// <param name="name">Name of the AniiMod</param>
        /// <param name="version">Version of the AniiMod</param>
        /// <param name="author">Author of the AniiMod</param>
        /// <param name="downloadLink">URL to the download link of the mod [optional]</param>
        public AniiModInfoAttribute(Type type, string name, string version, string author, string downloadLink = null) 
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            SystemType = type;
            Name = name ?? "UNKNOWN";
            Author = author ?? "UNKNOWN";
            DownloadLink = downloadLink; // Might get Removed. Not sure yet.

            if (string.IsNullOrEmpty(version))
                Version = "1.0.0";
            else
                Version = version;

            if (SemVersion.TryParse(Version, out SemVersion semver))
                SemanticVersion = semver;
        }

        /// <summary>
        /// AniiModInfo constructor.
        /// </summary>
        /// <param name="type">The main AniiMod type of the AniiMod (for example TestMod)</param>
        /// <param name="name">Name of the AniiMod</param>
        /// <param name="versionMajor">Version Major of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="versionMinor">Version Minor of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="versionRevision">Version Revision of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="versionIdentifier">Version Identifier of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="author">Author of the AniiMod</param>
        /// <param name="downloadLink">URL to the download link of the mod [optional]</param>
        public AniiModInfoAttribute(Type type, string name, int versionMajor, int versionMinor, int versionRevision, string versionIdentifier, string author, string downloadLink = null)
            : this(type, name, $"{versionMajor}.{versionMinor}.{versionRevision}{(string.IsNullOrEmpty(versionIdentifier) ? "" : versionIdentifier)}", author, downloadLink) { }

        /// <summary>
        /// AniiModInfo constructor.
        /// </summary>
        /// <param name="type">The main AniiMod type of the AniiMod (for example TestMod)</param>
        /// <param name="name">Name of the AniiMod</param>
        /// <param name="versionMajor">Version Major of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="versionMinor">Version Minor of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="versionRevision">Version Revision of the AniiMod (Using the <see href="https://semver.org">Semantic Versioning</see> format)</param>
        /// <param name="author">Author of the AniiMod</param>
        /// <param name="downloadLink">URL to the download link of the mod [optional]</param>
        public AniiModInfoAttribute(Type type, string name, int versionMajor, int versionMinor, int versionRevision, string author, string downloadLink = null)
            : this(type, name, versionMajor, versionMinor, versionRevision, null, author, downloadLink) { }
    }
}