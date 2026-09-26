using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModOptionalDependenciesAttribute : Attribute
    {
        /// <summary>
        /// The (simple) assembly names of the dependencies that should be regarded as optional.
        /// </summary>
        public string[] AssemblyNames { get; internal set; }

        public AniiModOptionalDependenciesAttribute(params string[] assemblyNames) { AssemblyNames = assemblyNames; }
    }
}