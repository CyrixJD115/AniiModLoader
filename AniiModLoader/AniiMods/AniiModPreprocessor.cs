using System;
using System.Collections.Generic;
using System.IO;
using AniiModLoader.Logging;
using Mono.Cecil;
using MonoMod.Utils;

namespace AniiModLoader.AniiMods
{
    internal static class AniiModPreprocessor
    {
        internal static void LoadFolders(List<string> directoryPaths,
            bool isAniiMod,
            ref bool hasWroteLine,
            ref List<AniiModAssembly> aniimodAssemblies)
        {
            // Find All Assemblies
            Dictionary<string, LemonTuple<Version, string>> foundAssemblies = new();
            foreach (string path in directoryPaths)
                PreprocessFolder(path, ref foundAssemblies, isAniiMod);

            // Load from File Paths
            foreach (var foundFile in foundAssemblies)
            {
                // Log
                if (!hasWroteLine)
                {
                    hasWroteLine = true;
                    AniiModLogger.WriteLine(ColorARGB.Magenta);
                }

                // Load Assembly
                var asm = AniiModAssembly.LoadAniiModAssembly(foundFile.Value.Item2, false);
                if (asm == null)
                    continue;

                // Queue Assembly for AniiMod Parsing
                if (isAniiMod)
                    aniimodAssemblies.Add(asm);
            }
        }

        private static void PreprocessFolder(string path,
            ref Dictionary<string, LemonTuple<Version, string>> foundAssemblies,
            bool isAniiMod)
        {
            // Validate Path
            if (!Directory.Exists(path))
                return;

            // Get DLLs in Directory
            var files = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                // Ignore Native DLLs
                if (!AniiModUtils.IsManagedDLL(f))
                    continue;

                // Load Definition using Cecil
                AssemblyDefinition asmDef = LoadDefinition(f);
                if (asmDef == null)
                    continue;
                if (isAniiMod && !asmDef.HasCustomAttribute(typeof(AniiModInfoAttribute).FullName))
				{
					asmDef.Dispose();
                    continue;
				}

                // Pull Name and Version from AssemblyDefinitionName
                string name = $"{asmDef.Name.Name}";
                Version version = new(asmDef.Name.Version.ToString());

                // Dispose of Definition
                asmDef.Dispose();

                // Check for Existing Version
                if (foundAssemblies.TryGetValue(name, out LemonTuple<Version, string> existingVersion)
                    && (existingVersion.Item1 >= version))
                    continue;

                // Add File to List
                foundAssemblies[name] = new(version, f);
            }
        }

        private static AssemblyDefinition LoadDefinition(string path)
        {
            path = Path.GetFullPath(path);

            try
            {
                return AssemblyDefinition.ReadAssembly(path);
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to load AssemblyDefinition from '{path}':\n{ex}");
                return null;
            }
        }
    }
}
