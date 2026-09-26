using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    public static class AniiModHandler
    {
        /// <summary>
        /// Directory of Plugins.
        /// </summary>
        [Obsolete("Use AniiModEnvironment.PluginsDirectory instead. This will be removed in a future update.", true)]
        public static string PluginsDirectory => AniiModEnvironment.PluginsDirectory;

        /// <summary>
        /// Directory of Mods.
        /// </summary>
        [Obsolete("Use AniiModEnvironment.ModsDirectory instead. This will be removed in a future update.", true)]
        public static string ModsDirectory => AniiModEnvironment.ModsDirectory;

        internal static void Setup()
        {
            if (!Directory.Exists(AniiModEnvironment.PluginsDirectory))
                Directory.CreateDirectory(AniiModEnvironment.PluginsDirectory);
            
            if (!Directory.Exists(AniiModEnvironment.ModsDirectory))
                Directory.CreateDirectory(AniiModEnvironment.ModsDirectory);
        }

        #region Obsolete Members
        /// <summary>
        /// List of Plugins.
        /// </summary>
        [Obsolete("Use 'AniiModPlugin.RegisteredAniiMods' instead. This will be removed in a future update.", true)]
        public static List<AniiModPlugin> Plugins => AniiModTypeBase<AniiModPlugin>.RegisteredAniiMods.ToList();

        /// <summary>
        /// List of Mods.
        /// </summary>
        [Obsolete("Use 'AniiModMod.RegisteredAniiMods' instead. This will be removed in a future update.", true)]
        public static List<AniiModMod> Mods => AniiModTypeBase<AniiModMod>.RegisteredAniiMods.ToList();

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromFile(string filelocation, bool is_plugin) => LoadFromFile(filelocation);

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromByteArray(byte[] filedata, string filelocation) => LoadFromByteArray(filedata, filepath: filelocation);

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromByteArray(byte[] filedata, string filelocation, bool is_plugin) => LoadFromByteArray(filedata, filepath: filelocation);

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromAssembly(Assembly asm, string filelocation, bool is_plugin) => LoadFromAssembly(asm, filelocation);

        [Obsolete("Use 'AniiModBase.Hash' instead. This will be removed in a future update.", true)]
        public static string GetAniiModHash(AniiModBase aniimodBase)
            => aniimodBase.Hash;

        [Obsolete("Use 'AniiModBase.RegisteredAniiMods.Exists(1)' instead. This will be removed in a future update.", true)]
        public static bool IsAniiModAlreadyLoaded(string name)
            => AniiModBase._registeredAniiMods.Exists(x => x.Info.Name == name);

        [Obsolete("Use 'AniiModPlugin.RegisteredAniiMods.Exists(1)' instead. This will be removed in a future update.", true)]
        public static bool IsPluginAlreadyLoaded(string name)
            => AniiModTypeBase<AniiModPlugin>._registeredAniiMods.Exists(x => x.Info.Name == name);

        [Obsolete("Use 'AniiModMod.RegisteredAniiMods.Exists(1)' instead. This will be removed in a future update.", true)]
        public static bool IsModAlreadyLoaded(string name)
            => AniiModTypeBase<AniiModMod>._registeredAniiMods.Exists(x => x.Info.Name == name);

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromFile(string filepath, string symbolspath = null)
        {
            var asm = AniiModAssembly.LoadAniiModAssembly(filepath);
            if (asm == null)
                return;

            AniiModBase.RegisterSorted(asm.LoadedAniiMods);
        }

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromByteArray(byte[] filedata, byte[] symbolsdata = null, string filepath = null)
        {
            var asm = AniiModAssembly.LoadRawAniiModAssembly(filepath, filedata, symbolsdata);
            if (asm == null)
                return;

            AniiModBase.RegisterSorted(asm.LoadedAniiMods);
        }

        [Obsolete("Use 'AniiModBase.Load' and 'AniiModBase.Register' instead. This will be removed in a future update.", true)]
        public static void LoadFromAssembly(Assembly asm, string filepath = null)
        {
            var ma = AniiModAssembly.LoadAniiModAssembly(filepath, asm);
            if (ma == null)
                return;

            AniiModBase.RegisterSorted(ma.LoadedAniiMods);
        }
        #endregion
    }
}
