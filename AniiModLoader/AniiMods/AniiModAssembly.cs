using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using AniiModLoader.Logging;
using AniiModLoader.Utils;
#if NET6_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace AniiModLoader
{
    public sealed class AniiModAssembly
    {
        #region Static

        /// <summary>
        /// Called before a process of resolving AniiMods from a AniiModAssembly has started.
        /// </summary>
        public static readonly AniiModEvent<Assembly> OnAssemblyResolving = new();

        public static event LemonFunc<Assembly, ResolvedAniiMods> CustomAniiModResolvers;

        internal static List<AniiModAssembly> loadedAssemblies = new();

        /// <summary>
        /// List of all loaded AniiModAssemblies.
        /// </summary>
        public static ReadOnlyCollection<AniiModAssembly> LoadedAssemblies => loadedAssemblies.AsReadOnly();

        /// <summary>
        /// Tries to find the instance of AniiMod with type T, whether it's registered or not
        /// </summary>
        public static T FindAniiModInstance<T>() where T : AniiModBase
        {
            foreach (var asm in loadedAssemblies)
            {
                foreach (var aniimod in asm.loadedAniiMods)
                {
                    if (aniimod is T teaAniiMod)
                        return teaAniiMod;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the AniiModAssembly of the given member. If the given member is not in any AniiModAssembly, returns null.
        /// </summary>
        public static AniiModAssembly GetAniiModAssemblyOfMember(MemberInfo member, object obj = null)
        {
            if (member == null)
                return null;

            if (obj != null && obj is AniiModBase aniimod)
                return aniimod.AniiModAssembly;

            var name = member.DeclaringType.Assembly.FullName;
            var ma = loadedAssemblies.Find(x => x.Assembly.FullName == name);
            return ma;
        }

        /// <summary>
        /// Loads or finds a AniiModAssembly from path.
        /// </summary>
        /// <param name="path">Path of the AniiModAssembly</param>
        /// <param name="loadAniiMods">Sets whether AniiMods should be auto-loaded or not</param>
        public static AniiModAssembly LoadAniiModAssembly(string path, bool loadAniiMods = true)
        {
            if (path == null)
            {
                AniiModLogger.Error("Failed to load a AniiMod Assembly: Path cannot be null.");
                return null;
            }

            path = Path.GetFullPath(path);

            try
            {
#if NET6_0_OR_GREATER
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#else
                var assembly = Assembly.LoadFrom(path);
#endif
                return LoadAniiModAssembly(path, assembly, loadAniiMods);
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to load AniiMod Assembly from '{path}':\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// Loads or finds a AniiModAssembly from raw Assembly Data.
        /// </summary>
        public static AniiModAssembly LoadRawAniiModAssembly(string path, byte[] assemblyData, byte[] symbolsData = null, bool loadAniiMods = true)
        {
            if (assemblyData == null)
            {
                AniiModLogger.Error("Failed to load a AniiMod Assembly: assemblyData cannot be null.");
                return null;
            }

            try
            {
#if NET6_0_OR_GREATER
                var fileStream = new MemoryStream(assemblyData);
                var symStream = symbolsData == null ? null : new MemoryStream(symbolsData);

                var assembly = AssemblyLoadContext.Default.LoadFromStream(fileStream, symStream);
#else
                var assembly = symbolsData != null ? Assembly.Load(assemblyData, symbolsData) : Assembly.Load(assemblyData);
#endif
                return LoadAniiModAssembly(path, assembly, loadAniiMods);
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to load AniiMod Assembly from raw Assembly Data (length {assemblyData.Length}):\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// Loads or finds a AniiModAssembly.
        /// </summary>
        public static AniiModAssembly LoadAniiModAssembly(string path, Assembly assembly, bool loadAniiMods = true)
        {
            if (!File.Exists(path))
                path = assembly.Location;

            if (assembly == null)
            {
                AniiModLogger.Error("Failed to load a AniiMod Assembly: Assembly cannot be null.");
                return null;
            }

            var ma = loadedAssemblies.Find(x => x.Assembly.FullName == assembly.FullName);
            if (ma != null)
                return ma;

            var shortPath = path;
            if (shortPath.StartsWith(AniiModEnvironment.AniiModBaseDirectory))
                shortPath = "." + shortPath.Remove(0, AniiModEnvironment.AniiModBaseDirectory.Length);

            OnAssemblyResolving.Invoke(assembly);
            ma = new AniiModAssembly(assembly, path);
            loadedAssemblies.Add(ma);

            if (loadAniiMods)
                ma.LoadAniiMods();

            AniiModLogger.MsgDirect(ColorARGB.DarkGray, $"AniiMod Assembly loaded: '{shortPath}'");
            AniiModLogger.MsgDirect(ColorARGB.DarkGray, $"SHA256 Hash: '{ma.Hash}'");
            return ma;
        }

        #endregion

        #region Instance

        private bool aniimodsLoaded;

        private readonly List<AniiModBase> loadedAniiMods = new();
        private readonly List<RottenAniiMod> rottenAniiMods = new();

        public readonly AniiModEvent OnUnregister = new();

        public bool HarmonyDontPatchAll { get; private set; } = true;

        /// <summary>
        /// A SHA256 Hash of the Assembly.
        /// </summary>
        public string Hash { get; private set; }

        public Assembly Assembly { get; private set; }

        public string Location { get; private set; }

        /// <summary>
        /// A list of all loaded AniiMods in the Assembly.
        /// </summary>
        public ReadOnlyCollection<AniiModBase> LoadedAniiMods => loadedAniiMods.AsReadOnly();

        /// <summary>
        /// A list of all broken AniiMods in the Assembly.
        /// </summary>
        public ReadOnlyCollection<RottenAniiMod> RottenAniiMods => rottenAniiMods.AsReadOnly();

        private AniiModAssembly(Assembly assembly, string location)
        {
            Assembly = assembly;
            Location = location ?? "";
            Hash = AniiModUtils.ComputeSimpleSHA256Hash(Location);
        }

        /// <summary>
        /// Unregisters all AniiMods in this Assembly.
        /// </summary>
        public void UnregisterAniiMods(string reason = null, bool silent = false)
        {
            foreach (var m in loadedAniiMods)
                m.UnregisterInstance(reason, silent);

            OnUnregister.Invoke();
        }

        private void OnApplicationQuit()
        {
            UnregisterAniiMods("AniiModLoader is deinitializing.", true);
        }

        private T SafeGetAttribute<T>(bool rotten = false)
            where T : Attribute
        {
            try
            {
                var att = AniiModUtils.PullAttributeFromAssembly<T>(Assembly);
                return att;
            }
            catch (Exception ex)
            {
                if (rotten)
                    rottenAniiMods.Add(new RottenAniiMod(Assembly, $"Failed to Pull Attribute '{typeof(T).Name}' from the AniiMod.", ex));
                else
                {
                    AniiModLogger.Error($"Failed to Pull Attribute '{typeof(T).Name}' from the AniiMod.");
                    AniiModLogger.Error(ex);
                }
                return null;
            }
        }

        private T[] SafeGetAllAttributes<T>(bool rotten = false)
            where T : Attribute
        {
            try
            {
                var att = AniiModUtils.PullAttributesFromAssembly<T>(Assembly);
                return att;
            }
            catch (Exception ex)
            {
                if (rotten)
                    rottenAniiMods.Add(new RottenAniiMod(Assembly, $"Failed to Pull Attribute '{typeof(T).Name}' from the AniiMod.", ex));
                else
                {
                    AniiModLogger.Error($"Failed to Pull Attribute '{typeof(T).Name}' from the AniiMod.");
                    AniiModLogger.Error(ex);
                }
                return null;
            }
        }

        public void LoadAniiMods()
        {
            if (aniimodsLoaded)
                return;

            aniimodsLoaded = true;

            AniiModEvents.OnApplicationDefiniteQuit.Subscribe(OnApplicationQuit);

            // \/ Custom Resolver \/
            var resolvers = CustomAniiModResolvers?.GetInvocationList();
            if (resolvers != null)
                foreach (LemonFunc<Assembly, ResolvedAniiMods> r in resolvers)
                {
                    var customAniiMod = r.Invoke(Assembly);

                    loadedAniiMods.AddRange(customAniiMod.loadedAniiMods);
                    rottenAniiMods.AddRange(customAniiMod.rottenAniiMods);
                }


            // \/ Default resolver \/
            var baseType = typeof(AniiModBase);
            var info = SafeGetAttribute<AniiModInfoAttribute>(true);
            if ((info != null)
                && (info.SystemType != null)
                && info.SystemType.IsSubclassOf(baseType))
            {
                var baseTypeMod = typeof(AniiModMod);
                var baseTypePlugin = typeof(AniiModPlugin);
                if ((info.SystemType == baseType)
                    || (info.SystemType == baseTypeMod)
                    || (info.SystemType == baseTypePlugin))
                {
                    rottenAniiMods.Add(new RottenAniiMod(Assembly, $"{info.SystemType.FullName} cannot be used for AniiModInfoAttribute.SystemType"));
                    return;
                }

                AniiModBase aniimod;
                try
                {
                    aniimod = (AniiModBase)Activator.CreateInstance(info.SystemType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null);
                }
                catch (Exception ex)
                {
                    aniimod = null;
                    rottenAniiMods.Add(new RottenAniiMod(info.SystemType, "Failed to create an instance of the AniiMod.", ex));
                }

                if (aniimod != null)
                {
                    var priorityAttr = SafeGetAttribute<AniiModPriorityAttribute>();
                    var colorAttr = SafeGetAttribute<AniiModColorAttribute>();
                    var authorColorAttr = SafeGetAttribute<AniiModAuthorColorAttribute>();
                    var additionalCreditsAttr = SafeGetAttribute<AniiModAdditionalCreditsAttribute>();
                    var procAttrs = SafeGetAllAttributes<AniiModProcessAttribute>();
                    var gameAttrs = SafeGetAllAttributes<AniiModGameAttribute>();
                    var optionalDependenciesAttr = SafeGetAttribute<AniiModOptionalDependenciesAttribute>();
                    var idAttr = SafeGetAttribute<AniiModIDAttribute>();
                    var gameVersionAttrs = SafeGetAllAttributes<AniiModGameVersionAttribute>();
                    var platformAttr = SafeGetAttribute<AniiModPlatformAttribute>();
                    var domainAttr = SafeGetAttribute<AniiModPlatformDomainAttribute>();
                    var mlVersionAttr = SafeGetAttribute<VerifyLoaderVersionAttribute>();
                    var mlBuildAttr = SafeGetAttribute<VerifyLoaderBuildAttribute>();
                    var harmonyDPAAttr = SafeGetAttribute<HarmonyDontPatchAllAttribute>();

                    aniimod.Info = info;
                    aniimod.AdditionalCredits = additionalCreditsAttr;
                    aniimod.AniiModAssembly = this;
                    aniimod.Priority = priorityAttr?.Priority ?? 0;
                    aniimod.ConsoleColor = colorAttr?.DrawingColor ?? AniiModLogger.DefaultAniiModColor;
                    aniimod.AuthorConsoleColor = authorColorAttr?.DrawingColor ?? AniiModLogger.DefaultTextColor;
                    aniimod.SupportedProcesses = procAttrs;
                    aniimod.Games = gameAttrs;
                    aniimod.SupportedGameVersions = gameVersionAttrs;
                    aniimod.SupportedPlatforms = platformAttr;
                    aniimod.SupportedDomain = domainAttr;
                    aniimod.SupportedMLVersion = mlVersionAttr;
                    aniimod.SupportedMLBuild = mlBuildAttr;
                    aniimod.OptionalDependencies = optionalDependenciesAttr;
                    aniimod.ID = idAttr?.ID;
                    HarmonyDontPatchAll = harmonyDPAAttr != null;

                    loadedAniiMods.Add(aniimod);

                    if (!SemVersion.TryParse(info.Version, out _))
                        AniiModLogger.Warning($"==Normal users can ignore this warning==\nAniiMod '{info.Name}' by '{info.Author}' has version '{info.Version}' which does not use the Semantic Versioning format. Versions using formats other than the Semantic Versioning format will not be supported in the future versions of AniiModLoader.\nFor more details, see: https://semver.org");
                }
            }

#if NET6_0_OR_GREATER
            RegisterTypeInIl2Cpp.RegisterAssembly(Assembly);
            RegisterTypeInIl2CppWithInterfaces.RegisterAssembly(Assembly);
#endif

            if (rottenAniiMods.Count != 0)
            {
                AniiModLogger.Error($"Failed to load {rottenAniiMods.Count} {"AniiMod".MakePlural(rottenAniiMods.Count)} from {Path.GetFileName(Location)}:");
                foreach (var r in rottenAniiMods)
                {
                    if (r.type != null)
                        AniiModLogger.Error($"Failed to load AniiMod '{r.type.FullName}': {r.errorMessage}");
                    else if (r.assembly != null)
                        AniiModLogger.Error($"Failed to load AniiMod '{r.assembly.GetName().Name}': {r.errorMessage}");
                    if (!string.IsNullOrEmpty(r.exception))
                        AniiModLogger.Error(r.exception);
                }
            }
        }

        #endregion
    }
}