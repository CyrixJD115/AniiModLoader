using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using AniiModLoader.Resolver;

#if NET6_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace AniiModLoader.InternalUtils
{
    internal class DependencyGraph<T> where T : AniiModBase
    {
        public static void TopologicalSort(IList<T> aniimods)
        {
            if (aniimods.Count <= 0)
                return;
            DependencyGraph<T> dependencyGraph = new DependencyGraph<T>(aniimods);
            aniimods.Clear();
            dependencyGraph.TopologicalSortInto(aniimods);
        }

        private readonly Vertex[] vertices;

        private DependencyGraph(IList<T> aniimods)
        {
            int size = aniimods.Count;
            vertices = new Vertex[size];
            Dictionary<string, Vertex> nameLookup = new Dictionary<string, Vertex>(size);

            // Create a vertex in the dependency graph for each AniiMod to load
            for (int i = 0; i < size; ++i)
            {
                Assembly aniimodAssembly = aniimods[i].AniiModAssembly.Assembly;
                string aniimodName = aniimods[i].Info.Name;

                Vertex aniimodVertex = new Vertex(i, aniimods[i], aniimodName);
                vertices[i] = aniimodVertex;
                nameLookup[aniimodAssembly.GetName().Name] = aniimodVertex;
            }

            // Add an edge for each dependency between AniiMods
            SortedDictionary<string, IList<AssemblyName>> aniimodsWithMissingDeps = new SortedDictionary<string, IList<AssemblyName>>();
            SortedDictionary<string, IList<AssemblyName>> aniimodsWithIncompatibilities = new SortedDictionary<string, IList<AssemblyName>>();
            List<AssemblyName> missingDependencies = new List<AssemblyName>();
            List<AssemblyName> incompatibilities = new List<AssemblyName>();
            HashSet<string> optionalDependencies = new HashSet<string>();
            HashSet<string> additionalDependencies = new HashSet<string>();

            foreach (Vertex aniimodVertex in vertices)
            {
                Assembly aniimodAssembly = aniimodVertex.aniimod.AniiModAssembly.Assembly;
                missingDependencies.Clear();
                optionalDependencies.Clear();
                incompatibilities.Clear();
                additionalDependencies.Clear();

                AniiModOptionalDependenciesAttribute optionals = (AniiModOptionalDependenciesAttribute)Attribute.GetCustomAttribute(aniimodAssembly, typeof(AniiModOptionalDependenciesAttribute));
                if (optionals != null
                    && optionals.AssemblyNames != null)
                    optionalDependencies.UnionWith(optionals.AssemblyNames);

                AniiModAdditionalDependenciesAttribute additionals = (AniiModAdditionalDependenciesAttribute)Attribute.GetCustomAttribute(aniimodAssembly, typeof(AniiModAdditionalDependenciesAttribute));
                if (additionals != null
                    && additionals.AssemblyNames != null)
                    additionalDependencies.UnionWith(additionals.AssemblyNames);

                AniiModIncompatibleAssembliesAttribute incompatibleAssemblies = (AniiModIncompatibleAssembliesAttribute)Attribute.GetCustomAttribute(aniimodAssembly, typeof(AniiModIncompatibleAssembliesAttribute));
                if (incompatibleAssemblies != null
                    && incompatibleAssemblies.AssemblyNames != null)
                {
                    foreach (string name in incompatibleAssemblies.AssemblyNames)
                        foreach (Vertex v in vertices)
                        {
                            AssemblyName assemblyName = v.aniimod.AniiModAssembly.Assembly.GetName();
                            if (v != aniimodVertex
                                && assemblyName.Name == name)
                            {
                                incompatibilities.Add(assemblyName);
                                v.skipLoading = true;
                            }
                        }
                }

                foreach (AssemblyName dependency in aniimodAssembly.GetReferencedAssemblies())
                {
                    if (nameLookup.TryGetValue(dependency.Name, out Vertex dependencyVertex))
                    {
                        if (!aniimodVertex.dependencies.Contains(dependencyVertex))
                            aniimodVertex.dependencies.Add(dependencyVertex);
                        if (!dependencyVertex.dependents.Contains(aniimodVertex))
                            dependencyVertex.dependents.Add(aniimodVertex);
                    }
                    else if (!TryLoad(dependency)
                        && !TryResolve(dependency)
                        && !optionalDependencies.Contains(dependency.Name)
                        && !missingDependencies.Contains(dependency))
                        missingDependencies.Add(dependency);
                }

                foreach (string dependencyName in additionalDependencies)
                {
                    AssemblyName dependency = new AssemblyName(dependencyName);
                    if (nameLookup.TryGetValue(dependencyName, out Vertex dependencyVertex))
                    {
                        if (!aniimodVertex.dependencies.Contains(dependencyVertex))
                            aniimodVertex.dependencies.Add(dependencyVertex);
                        if (!dependencyVertex.dependents.Contains(aniimodVertex))
                            dependencyVertex.dependents.Add(aniimodVertex);
                    }
                    else if (!TryLoad(dependency)
                        && !TryResolve(dependency)
                        && !missingDependencies.Contains(dependency))
                        missingDependencies.Add(dependency);
                }

                if ((missingDependencies.Count > 0) 
                    && !aniimodsWithMissingDeps.ContainsKey(aniimodVertex.aniimod.Info.Name))
                    // aniimodVertex.skipLoading = true;
                    aniimodsWithMissingDeps.Add(aniimodVertex.aniimod.Info.Name, missingDependencies.ToArray());

                if ((incompatibilities.Count > 0)
                    && !aniimodsWithIncompatibilities.ContainsKey(aniimodVertex.aniimod.Info.Name))
                    aniimodsWithIncompatibilities.Add(aniimodVertex.aniimod.Info.Name, incompatibilities.ToArray());
            }

            // Some AniiMods are missing dependencies. Don't load these AniiMods and show an error message
            if (aniimodsWithMissingDeps.Count > 0)
                AniiModLogger.Warning(BuildMissingDependencyMessage(aniimodsWithMissingDeps));

            if (aniimodsWithIncompatibilities.Count > 0)
                AniiModLogger.Warning(BuildIncompatibleAssembliesMessage(aniimodsWithIncompatibilities));
        }

        // Returns true if 'assembly' was already loaded or could be loaded, false if the required assembly was missing.
        private static bool TryLoad(AssemblyName assembly)
        {
            try
            {
#if NET6_0_OR_GREATER
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyName(assembly);
#else
                var asm = Assembly.Load(assembly);
#endif

                if (asm == null)
                    return false;
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (Exception ex)
            {
                AniiModLogger.Error("Loading AniiMod Dependency Failed: " + ex);
                return false;
            }
        }
		
		// Returns true if 'assembly' was already resolved or could be resolved, false if the required assembly was missing.
        private static bool TryResolve(AssemblyName assembly)
        {
            try
            {
                Assembly asm = SearchDirectoryManager.Scan(assembly.Name);
                if (asm == null)
                    return false;
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (Exception ex)
            {
                AniiModLogger.Error("Resolving AniiMod Dependency Failed: " + ex);
                return false;
            }
        }

        private static string BuildMissingDependencyMessage(IDictionary<string, IList<AssemblyName>> aniimodsWithMissingDeps)
        {
            StringBuilder messageBuilder = new StringBuilder("Some AniiMods are missing dependencies, which you may have to install.\n" +
                "If these are optional dependencies, mark them as optional using the AniiModOptionalDependencies attribute.\n" +
                "This warning will turn into an error and AniiMods with missing dependencies will not be loaded in future versions of AniiModLoader.\n");
            foreach (string aniimodName in aniimodsWithMissingDeps.Keys)
            {
                messageBuilder.Append($"- '{aniimodName}' is missing the following dependencies:\n");
                foreach (AssemblyName dependency in aniimodsWithMissingDeps[aniimodName])
                    messageBuilder.Append($"    - '{dependency.Name}' v{dependency.Version}\n");
            }
            messageBuilder.Length -= 1; // Remove trailing newline
            return messageBuilder.ToString();
        }

        private static string BuildIncompatibleAssembliesMessage(IDictionary<string, IList<AssemblyName>> aniimodsWithIncompatibilities)
        {
            StringBuilder messageBuilder = new StringBuilder("Some AniiMods are marked as incompatible with each other.\n" +
                "To avoid any errors, these AniiMods will not be loaded.\n");
            foreach (string aniimodName in aniimodsWithIncompatibilities.Keys)
            {
                messageBuilder.Append($"- '{aniimodName}' is incompatible with the following AniiMods:\n");
                foreach (AssemblyName dependency in aniimodsWithIncompatibilities[aniimodName])
                {
                    messageBuilder.Append($"    - '{dependency.Name}'\n");
                }
            }
            messageBuilder.Length -= 1; // Remove trailing newline
            return messageBuilder.ToString();
        }

        private void TopologicalSortInto(IList<T> loadedAniiMods)
        {
            int[] unloadedDependencies = new int[vertices.Length];
            SortedList<string, Vertex> loadableAniiMods = new SortedList<string, Vertex>();
            int skippedAniiMods = 0;

            // Find all sinks in the dependency graph, i.e. AniiMods without any dependencies on other AniiMods
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vertex vertex = vertices[i];
                int dependencyCount = vertex.dependencies.Count;

                unloadedDependencies[i] = dependencyCount;
                if ((dependencyCount == 0) 
                    && !loadableAniiMods.ContainsKey(vertex.name))
                    loadableAniiMods.Add(vertex.name, vertex);
            }

            // Perform the (reverse) topological sorting
            while (loadableAniiMods.Count > 0)
            {
                Vertex aniimod = loadableAniiMods.Values[0];
                loadableAniiMods.RemoveAt(0);

                if (!aniimod.skipLoading
                    && !loadableAniiMods.ContainsKey(aniimod.name))
                    loadedAniiMods.Add(aniimod.aniimod);
                else
                    ++skippedAniiMods;

                foreach (Vertex dependent in aniimod.dependents)
                {
                    unloadedDependencies[dependent.index] -= 1;
                    dependent.skipLoading |= aniimod.skipLoading;

                    if ((unloadedDependencies[dependent.index] == 0)
                        && !loadableAniiMods.ContainsKey(dependent.name))
                        loadableAniiMods.Add(dependent.name, dependent);
                }
            }

            // Check if all AniiMods were either loaded or skipped. If this is not the case, there is a cycle in the dependency graph
            if (loadedAniiMods.Count + skippedAniiMods < vertices.Length)
            {
                StringBuilder errorMessage = new StringBuilder("Some AniiMods could not be loaded due to a cyclic dependency:\n");
                for (int i = 0; i < vertices.Length; ++i)
                    if (unloadedDependencies[i] > 0)
                        errorMessage.Append($"- '{vertices[i].name}'\n");
                errorMessage.Length -= 1; // Remove trailing newline
                AniiModLogger.Error(errorMessage.ToString());
            }
        }

        private class Vertex
        {
            internal readonly int index;
            internal readonly T aniimod;
            internal readonly string name;

            internal readonly List<Vertex> dependencies;
            internal readonly List<Vertex> dependents;
            internal bool skipLoading;

            internal Vertex(int index, T aniimod, string name)
            {
                this.index = index;
                this.aniimod = aniimod;
                this.name = name;

                dependencies = new List<Vertex>();
                dependents = new List<Vertex>();
                skipLoading = false;
            }
        }
    }
}