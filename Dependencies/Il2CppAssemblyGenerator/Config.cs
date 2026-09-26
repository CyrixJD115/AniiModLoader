using System;
using System.Collections.Generic;
using System.IO;
using AniiModLoader.Preferences;

namespace AniiModLoader.Il2CppAssemblyGenerator
{
    internal class Config
    {
        private static string FilePath;
        private static AniiModPreferences_ReflectiveCategory Category;
        internal static AssemblyGeneratorConfiguration Values;

        internal static void Initialize()
        {
            FilePath = Compatibility.LoaderPaths.MigrateConfig(Core.BasePath, "AniiModGenerator.cfg", "Config.cfg");

            Category = AniiModPreferences.CreateCategory<AssemblyGeneratorConfiguration>("Il2CppAssemblyGenerator");
            Category.SetFilePath(FilePath, printmsg: false);
            Category.DestroyFileWatcher();

            Values = Category.GetValue<AssemblyGeneratorConfiguration>();

            if (!File.Exists(FilePath))
                Save();
        }

        internal static void Save() => Category.SaveToFile(false);

        public class AssemblyGeneratorConfiguration
        {
            public string GameAssemblyHash = null;
            public string DeobfuscationRegex = null;
            public string UnityVersion = "0.0.0.0";
            public string DumperVersion = "0.0.0.0";
            public string DumperSCRSVersion = "0.0.0.0";
            public List<string> OldFiles = new List<string>();
        }
    }
}