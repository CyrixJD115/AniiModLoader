using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModIncompatibleAssembliesAttribute : Attribute
    {
        /// <summary>
        /// The (simple) assembly names of the mods that are incompatible.
        /// </summary>
        public string[] AssemblyNames { get; internal set; }

        public AniiModIncompatibleAssembliesAttribute(params string[] assemblyNames) { AssemblyNames = assemblyNames; }
    }
}