using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModAdditionalDependenciesAttribute : Attribute
    {
        /// <summary>
        /// The (simple) assembly names of Additional Dependencies that aren't directly referenced but should still be regarded as important.
        /// </summary>
        public string[] AssemblyNames { get; internal set; }

        public AniiModAdditionalDependenciesAttribute(params string[] assemblyNames) { AssemblyNames = assemblyNames; }
    }
}