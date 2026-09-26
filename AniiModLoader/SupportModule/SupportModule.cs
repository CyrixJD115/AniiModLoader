using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    internal static class SupportModule
    {
        internal static ISupportModule_To Interface = null;

        private static string BaseDirectory = null;
        private static List<ModuleListing> Modules = new List<ModuleListing>()
        {
            new ModuleListing("Il2Cpp.dll", AniiModUtils.IsGameIl2Cpp),
            new ModuleListing("Mono.dll", () => !AniiModUtils.IsGameIl2Cpp())
        };

        internal static bool Setup()
        {
            BaseDirectory = AniiModEnvironment.SupportModuleDirectory;
            if (!Directory.Exists(BaseDirectory))
            {
                AniiModLogger.Error("Failed to Find SupportModules Directory!");
                return false;
            }

            LemonEnumerator<ModuleListing> enumerator = new LemonEnumerator<ModuleListing>(Modules);
            while (enumerator.MoveNext())
            {
                string ModulePath = Path.Combine(BaseDirectory, enumerator.Current.FileName);
                if (!File.Exists(ModulePath))
                    continue;

                try
                {
                    if (enumerator.Current.LoadSpecifier != null)
                    {
                        if (!enumerator.Current.LoadSpecifier())
                        {
                            //File.Delete(ModulePath);
                            //string depsJson = Path.Combine(Path.GetDirectoryName(ModulePath), 
                            //    Path.GetFileNameWithoutExtension(ModulePath) + ".deps.json");
                            //if (File.Exists(depsJson))
                            //    File.Delete(depsJson);

                            continue;
                        }
                    }

                    if (Interface != null)
                        continue;

                    if (!LoadInterface(ModulePath))
                        continue;
                }
                catch (Exception ex)
                {
                    AniiModDebug.Error($"Support Module [{enumerator.Current.FileName}] threw an Exception: {ex}");
                    continue;
                }
            }

            if (Interface == null)
            {
                AniiModLogger.Error("No Support Module Loaded!");
                return false;
            }
            return true;
        }

        private static bool LoadInterface(string ModulePath)
        {
            Assembly assembly = Assembly.LoadFrom(ModulePath);
            if (assembly == null)
                return false;

            Type type = assembly.GetType("AniiModLoader.Support.Main");
            if (type == null)
            {
                AniiModLogger.Error("Failed to Get Type AniiModLoader.Support.Main!");
                return false;
            }

            MethodInfo method = type.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                AniiModLogger.Error("Failed to Get Method Initialize!");
                return false;
            }

            Interface = (ISupportModule_To)method.Invoke(null, new object[] { new SupportModule_From() });
            if (Interface == null)
            {
                AniiModLogger.Error("Failed to Initialize Interface!");
                return false;
            }

            AniiModLogger.Msg($"Support Module Loaded: {ModulePath}");

            return true;
        }

        // Module Listing
        private class ModuleListing
        {
            internal string FileName = null;
            internal delegate bool dLoadSpecifier();
            internal dLoadSpecifier LoadSpecifier = null;
            internal ModuleListing(string filename)
                => FileName = filename;
            internal ModuleListing(string filename, dLoadSpecifier loadSpecifier)
            {
                FileName = filename;
                LoadSpecifier = loadSpecifier;
            }
        }
    }
}