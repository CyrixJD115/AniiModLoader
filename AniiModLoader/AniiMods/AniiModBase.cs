using AniiModLoader.InternalUtils;
using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AniiModLoader.Logging;
using HarmonyLib;

#pragma warning disable 0618

namespace AniiModLoader
{
    public abstract class AniiModBase
    {
        #region Static

        /// <summary>
        /// Called once a AniiMod is fully registered.
        /// </summary>
        public static readonly AniiModEvent<AniiModBase> OnAniiModRegistered = new();

        /// <summary>
        /// Called when a AniiMod unregisters.
        /// </summary>
        public static readonly AniiModEvent<AniiModBase> OnAniiModUnregistered = new();

        /// <summary>
        /// Called before a AniiMod starts initializing.
        /// </summary>
        public static readonly AniiModEvent<AniiModBase> OnAniiModInitializing = new();

        public static ReadOnlyCollection<AniiModBase> RegisteredAniiMods => _registeredAniiMods.AsReadOnly();
        internal static List<AniiModBase> _registeredAniiMods = new();

        /// <summary>
        /// Creates a new AniiMod instance for a Wrapper.
        /// </summary>
        public static T CreateWrapper<T>(string name, string author, string version, AniiModGameAttribute[] games = null, AniiModProcessAttribute[] processes = null, int priority = 0, ColorARGB? color = null, ColorARGB? authorColor = null, string id = null) where T : AniiModBase, new()
        {
            var aniimod = new T
            {
                Info = new AniiModInfoAttribute(typeof(T), name, version, author),
                AniiModAssembly = AniiModAssembly.LoadAniiModAssembly(null, typeof(T).Assembly),
                Priority = priority,
                ConsoleColor = color ?? AniiModLogger.DefaultAniiModColor,
                AuthorConsoleColor = authorColor ?? AniiModLogger.DefaultTextColor,
                SupportedProcesses = processes,
                Games = games,
                OptionalDependencies = null,
                ID = id
            };

            return aniimod;
        }

        /// <summary>
        /// Registers a List of AniiMods in the right order.
        /// </summary>
        public static void RegisterSorted<T>(IEnumerable<T> aniimods) where T : AniiModBase
        {
            if (aniimods == null)
                return;

            var collection = aniimods.ToList();
            SortAniiMods(ref collection);

            foreach (var m in collection)
                m.Register();
        }

        private static void SortAniiMods<T>(ref List<T> aniimods) where T : AniiModBase
        {
            DependencyGraph<T>.TopologicalSort(aniimods);
            aniimods = aniimods.OrderBy(x => x.Priority).ToList();
        }

        #endregion

        #region Instance

        private AniiModGameAttribute[] _games = new AniiModGameAttribute[0];
        private AniiModProcessAttribute[] _processes = new AniiModProcessAttribute[0];
        private AniiModGameVersionAttribute[] _gameVersions = new AniiModGameVersionAttribute[0];

        public readonly AniiModEvent OnRegister = new();
        public readonly AniiModEvent OnUnregister = new();

        /// <summary>
        /// AniiModAssembly of the AniiMod.
        /// </summary>
        public AniiModAssembly AniiModAssembly { get; internal set; }

        /// <summary>
        /// Priority of the AniiMod.
        /// </summary>
        public int Priority { get; internal set; }

        /// <summary>
        /// Console Color of the AniiMod.
        /// </summary>
        public ColorARGB ConsoleColor { get; internal set; }

        /// <summary>
        /// Console Color of the Author that made this aniimod.
        /// </summary>
        public ColorARGB AuthorConsoleColor { get; internal set; }

        /// <summary>
        /// Info Attribute of the AniiMod.
        /// </summary>
        public AniiModInfoAttribute Info { get; internal set; }

        /// <summary>
        /// AdditionalCredits Attribute of the AniiMod
        /// </summary>
        public AniiModAdditionalCreditsAttribute AdditionalCredits { get; internal set; }

        /// <summary>
        /// Process Attributes of the AniiMod.
        /// </summary>
        public AniiModProcessAttribute[] SupportedProcesses
        {
            get => _processes;
            internal set => _processes = (value == null || value.Any(x => x.Universal)) ? new AniiModProcessAttribute[0] : value;
        }

        /// <summary>
        /// Game Attributes of the AniiMod.
        /// </summary>
        public AniiModGameAttribute[] Games
        {
            get => _games;
            internal set => _games = (value == null || value.Any(x => x.Universal)) ? new AniiModGameAttribute[0] : value;
        }

        /// <summary>
        /// Game Version Attributes of the AniiMod.
        /// </summary>
        public AniiModGameVersionAttribute[] SupportedGameVersions
        {
            get => _gameVersions;
            internal set => _gameVersions = (value == null || value.Any(x => x.Universal)) ? new AniiModGameVersionAttribute[0] : value;
        }

        /// <summary>
        /// Optional Dependencies Attribute of the AniiMod.
        /// </summary>
        public AniiModOptionalDependenciesAttribute OptionalDependencies { get; internal set; }

        /// <summary>
        /// Platform Attribute of the AniiMod.
        /// </summary>
        public AniiModPlatformAttribute SupportedPlatforms { get; internal set; }

        /// <summary>
        /// Platform Attribute of the AniiMod.
        /// </summary>
        public AniiModPlatformDomainAttribute SupportedDomain { get; internal set; }

        /// <summary>
        /// Verify Loader Version Attribute of the AniiMod.
        /// </summary>
        public VerifyLoaderVersionAttribute SupportedMLVersion { get; internal set; }

        /// <summary>
        /// Verify Build Version Attribute of the AniiMod.
        /// </summary>
        public VerifyLoaderBuildAttribute SupportedMLBuild { get; internal set; }

        /// <summary>
        /// Auto-Created Harmony Instance of the AniiMod.
        /// </summary>
        public HarmonyLib.Harmony HarmonyInstance { get; internal set; }

        /// <summary>
        /// Auto-Created AniiModLogger Instance of the AniiMod.
        /// </summary>
        public AniiModLogger.Instance LoggerInstance { get; internal set; }

        /// <summary>
        /// Optional ID of the AniiMod.
        /// </summary>
        public string ID { get; internal set; }

        /// <summary>
        /// <see langword="true"/> if the AniiMod is registered.
        /// </summary>
        public bool Registered { get; private set; }

        /// <summary>
        /// Name of the current AniiMod Type.
        /// </summary>
        public abstract string AniiModTypeName { get; }

        #region Callbacks

        /// <summary>
        /// Runs before Support Module Initialization and after Assembly Generation for Il2Cpp Games.
        /// </summary>
        public virtual void OnPreSupportModule() { }

        /// <summary>
        /// Runs once per frame.
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// Can run multiple times per frame. Mostly used for Physics.
        /// </summary>
        public virtual void OnFixedUpdate() { }

        /// <summary>
        /// Runs once per frame, after <see cref="OnUpdate"/>.
        /// </summary>
        public virtual void OnLateUpdate() { }

        /// <summary>
        /// Can run multiple times per frame. Mostly used for Unity's IMGUI.
        /// </summary>
        public virtual void OnGUI() { }

        /// <summary>
        /// Runs on a quit request. It is possible to abort the request in this callback.
        /// </summary>
        public virtual void OnApplicationQuit() { }

        /// <summary>
        /// Runs when AniiMod Preferences get saved.
        /// </summary>
        public virtual void OnPreferencesSaved() { }

        /// <summary>
        /// Runs when AniiMod Preferences get saved. Gets passed the Preferences's File Path.
        /// </summary>
        public virtual void OnPreferencesSaved(string filepath) { }

        /// <summary>
        /// Runs when AniiMod Preferences get loaded.
        /// </summary>
        public virtual void OnPreferencesLoaded() { }

        /// <summary>
        /// Runs when AniiMod Preferences get loaded. Gets passed the Preferences's File Path.
        /// </summary>
        public virtual void OnPreferencesLoaded(string filepath) { }

        /// <summary>
        /// Runs when the AniiMod is registered. Executed before the AniiMod's info is printed to the console. This callback should only be used a constructor for the AniiMod.
        /// </summary>
        /// <remarks>
        /// Please note that this callback may run before the Support Module is loaded.
        /// <br>As a result, using unhollowed assemblies may not be possible yet and you would have to override <see cref="OnInitializeAniiMod"/> instead.</br>
        /// </remarks>
        public virtual void OnEarlyInitializeAniiMod() { }

        /// <summary>
        /// Runs after the AniiMod has registered. This callback waits until AniiModLoader has fully initialized (<see cref="AniiModEvents.OnApplicationStart"/>).
        /// </summary>
        public virtual void OnInitializeAniiMod() { }

        /// <summary>
        /// Runs after <see cref="OnInitializeAniiMod"/>. This callback waits until Unity has invoked the first 'Start' messages (<see cref="AniiModEvents.OnApplicationLateStart"/>).
        /// </summary>
        public virtual void OnLateInitializeAniiMod() { }

        /// <summary>
        /// Runs when the AniiMod is unregistered. Also runs before the Application is closed (<see cref="AniiModEvents.OnApplicationDefiniteQuit"/>).
        /// </summary>
        public virtual void OnDeinitializeAniiMod() { }

        #endregion

        public Incompatibility[] FindIncompatiblities(AniiModGameAttribute game, string processName, string gameVersion,
            string mlVersion, string mlBuildHashCode, AniiModPlatformAttribute.CompatiblePlatforms platform,
            AniiModPlatformDomainAttribute.CompatibleDomains domain)
            => FindIncompatiblities(game, processName, gameVersion, SemVersion.Parse(mlVersion), mlBuildHashCode, platform, domain);

        public Incompatibility[] FindIncompatiblities(AniiModGameAttribute game, string processName, string gameVersion,
            SemVersion mlVersion, string mlBuildHashCode, AniiModPlatformAttribute.CompatiblePlatforms platform,
            AniiModPlatformDomainAttribute.CompatibleDomains domain)
        {
            var result = new List<Incompatibility>();
            if (!(Games.Length == 0 || Games.Any(x => x.IsCompatible(game))))
                result.Add(Incompatibility.Game);
            else
            {
                if (!(SupportedGameVersions.Length == 0 || SupportedGameVersions.Any(x => x.Version == gameVersion)))
                    result.Add(Incompatibility.GameVersion);

                if (!(SupportedProcesses.Length == 0 || SupportedProcesses.Any(x => x.IsCompatible(processName))))
                    result.Add(Incompatibility.ProcessName);

                if (!(SupportedPlatforms == null || SupportedPlatforms.IsCompatible(platform)))
                    result.Add(Incompatibility.Platform);

                if (!(SupportedDomain == null || SupportedDomain.IsCompatible(domain)))
                    result.Add(Incompatibility.Domain);
            }
            if (!(SupportedMLVersion == null || SupportedMLVersion.IsCompatible(mlVersion)))
                result.Add(Incompatibility.MLVersion);
            else
            {
                if (!(SupportedMLBuild == null || SupportedMLBuild.IsCompatible(mlBuildHashCode)))
                    result.Add(Incompatibility.MLBuild);
            }

            return result.ToArray();
        }

        public Incompatibility[] FindIncompatiblitiesFromContext()
        {
            return FindIncompatiblities(AniiModUtils.CurrentGameAttribute,
                Process.GetCurrentProcess().ProcessName, 
                UnityInformationHandler.GameVersion, 
                Properties.BuildInfo.VersionNumber, 
                AniiModUtils.HashCode, 
                AniiModUtils.CurrentPlatform,
                AniiModUtils.CurrentDomain);
        }

        public static void PrintIncompatibilities(Incompatibility[] incompatibilities, AniiModBase aniimod)
        {
            if (incompatibilities == null || incompatibilities.Length == 0)
                return;

            AniiModLogger.WriteLine(ColorARGB.Red);
            AniiModLogger.MsgDirect(ColorARGB.DarkRed, $"'{aniimod.Info.Name} v{aniimod.Info.Version}' is incompatible:");
            if (incompatibilities.Contains(Incompatibility.Game))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following Games:");

                foreach (var g in aniimod.Games)
                    AniiModLogger.MsgDirect($"    - '{g.Name}' by {g.Developer}");
            }
            if (incompatibilities.Contains(Incompatibility.GameVersion))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following Game Versions:");

                foreach (var g in aniimod.SupportedGameVersions)
                    AniiModLogger.MsgDirect($"    - {g.Version}");
            }
            if (incompatibilities.Contains(Incompatibility.ProcessName))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following Process Names:");

                foreach (var p in aniimod.SupportedProcesses)
                    AniiModLogger.MsgDirect($"    - '{p.EXE_Name}'");
            }
            if (incompatibilities.Contains(Incompatibility.Platform))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following Platforms:");

                foreach (var p in aniimod.SupportedPlatforms.Platforms)
                    AniiModLogger.MsgDirect($"    - {p}");
            }
            if (incompatibilities.Contains(Incompatibility.Domain))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following Domain:");
                AniiModLogger.MsgDirect($"    - {aniimod.SupportedDomain.Domain}");
            }
            if (incompatibilities.Contains(Incompatibility.MLVersion))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name}  is only compatible with the following AniiModLoader Versions:");
                AniiModLogger.MsgDirect($"    - {aniimod.SupportedMLVersion.SemVer}{(aniimod.SupportedMLVersion.IsMinimum ? " or higher" : "")}");
            }
            if (incompatibilities.Contains(Incompatibility.MLBuild))
            {
                AniiModLogger.MsgDirect($"- {aniimod.Info.Name} is only compatible with the following AniiModLoader Build Hash Codes:");
                AniiModLogger.MsgDirect($"    - {aniimod.SupportedMLBuild.HashCode}");
            }

            AniiModLogger.WriteLine(ColorARGB.Red);
            AniiModLogger.WriteSpacer();
        }

        /// <summary>
        /// Registers the AniiMod.
        /// </summary>
        public bool Register()
        {
            if (Registered)
                return false;

            if (FindAniiMod(Info.Name, Info.Author) != null)
            {
                AniiModLogger.Warning($"Failed to register {AniiModTypeName} '{AniiModAssembly.Location}': A AniiMod with the same Name and Author is already registered!");
                return false;
            }

            var comp = FindIncompatiblitiesFromContext();
            if (comp.Length != 0)
            {
                PrintIncompatibilities(comp, this);
                return false;
            }

            OnAniiModInitializing.Invoke(this);

            LoggerInstance ??= new AniiModLogger.Instance(string.IsNullOrEmpty(ID) ? Info.Name : $"{ID}:{Info.Name}", ConsoleColor);
            HarmonyInstance ??= new HarmonyLib.Harmony($"{AniiModAssembly.Assembly.FullName}:{Info.Name}");

            Registered = true; // this has to be true before the aniimod can subscribe to any events
            RegisterCallbacks();

            try
            {
                OnEarlyInitializeAniiMod();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to register {AniiModTypeName} '{AniiModAssembly.Location}': AniiMod failed to initialize!");
                AniiModLogger.Error(ex.ToString());
                Registered = false;
                return false;
            }

            if (!RegisterInternal())
                return false;

            _registeredAniiMods.Add(this);

            PrintLoadInfo();

            OnRegister.Invoke();
            OnAniiModRegistered.Invoke(this);

            if (AniiModEvents.OnApplicationStart.Disposed)
                LoaderInitialized();
            else
                AniiModEvents.OnApplicationStart.Subscribe(LoaderInitialized, Priority, true);

            if (AniiModEvents.OnApplicationLateStart.Disposed)
                OnLateInitializeAniiMod();
            else
                AniiModEvents.OnApplicationLateStart.Subscribe(OnLateInitializeAniiMod, Priority, true);

            return true;
        }

        internal void HarmonyInit()
        {
            if (AniiModAssembly.HarmonyDontPatchAll)
                return;

            var allTypes = AniiModAssembly.Assembly.GetValidTypes();
            foreach (var type in allTypes)
            {
                try
                {
                    // Harmony.PatchAll(Assembly) sets allowUnannotatedType to false
                    // Harmony.PatchAll(Type) sets allowUnannotatedType to true
                    // This mimics the behavior of Harmony.PatchAll(Assembly)
                    var proc = HarmonyInstance.CreateClassProcessor(type, false);
                    var patches = proc.Patch();
                    if (AniiModDebug.IsEnabled() && (patches != null) && (patches.Count > 0))
                        LoggerInstance.Msg($"HarmonyInit PatchAll: {type.FullName}");
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"Failed to HarmonyInit PatchAll: {type.FullName}");
                    LoggerInstance.Error(ex);
                }
            }
        }

        private void LoaderInitialized()
        {
            try
            {
                OnInitializeAniiMod();
            }
            catch (Exception ex)
            {
                LoggerInstance.Error(ex);
            }
        }

        protected private virtual bool RegisterInternal()
        {
            return true;
        }

        protected private virtual void UnregisterInternal() { }

        protected private virtual void RegisterCallbacks()
        {
            AniiModEvents.OnApplicationQuit.Subscribe(OnApplicationQuit, Priority);
            AniiModEvents.OnUpdate.Subscribe(OnUpdate, Priority);
            AniiModEvents.OnLateUpdate.Subscribe(OnLateUpdate, Priority);
            AniiModEvents.OnGUI.Subscribe(OnGUI, Priority);
            AniiModEvents.OnFixedUpdate.Subscribe(OnFixedUpdate, Priority);

#pragma warning disable CS0612 // Type or member is obsolete
            RegisterObsoleteCallbacks();
#pragma warning restore CS0612 // Type or member is obsolete

            AniiModPreferences.OnPreferencesLoaded.Subscribe(PrefsLoaded, Priority);
            AniiModPreferences.OnPreferencesSaved.Subscribe(PrefsSaved, Priority);
        }

        [Obsolete]
        private void RegisterObsoleteCallbacks()
        {
            AniiModEvents.OnApplicationLateStart.Subscribe(OnApplicationLateStart, Priority);
        }

        private void PrefsSaved(string path)
        {
            OnPreferencesSaved(path);
            OnPreferencesSaved();

#pragma warning disable CS0612 // Type or member is obsolete
            PrefsSavedObsoleteCallback();
#pragma warning restore CS0612 // Type or member is obsolete
        }

        [Obsolete]
        private void PrefsSavedObsoleteCallback()
        {
            OnModSettingsApplied();
        }

        private void PrefsLoaded(string path)
        {
            OnPreferencesLoaded(path);
            OnPreferencesLoaded();
        }

        /// <summary>
        /// Tries to find a registered AniiMod that matches the given Info.
        /// </summary>
        public static AniiModBase FindAniiMod(string aniimodName, string aniimodAuthor)
        {
            return _registeredAniiMods.Find(x => x.Info.Name == aniimodName && x.Info.Author == aniimodAuthor);
        }

        /// <summary>
        /// Unregisters the AniiMod and all other AniiMods located in the same Assembly.
        /// <para>This only unsubscribes the AniiMods from all Callbacks/<see cref="AniiModEvent"/>s and unpatches all Methods that were patched by Harmony, but doesn't actually unload the whole Assembly.</para>
        /// </summary>
        public void Unregister(string reason = null, bool silent = false)
        {
            if (!Registered)
                return;

            AniiModAssembly.UnregisterAniiMods(reason, silent);
        }

        internal void UnregisterInstance(string reason, bool silent)
        {
            if (!Registered)
                return;

            try
            {
                OnDeinitializeAniiMod();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to properly unregister {AniiModTypeName} '{AniiModAssembly.Location}': AniiMod failed to deinitialize!");
                AniiModLogger.Error(ex.ToString());
            }

            UnregisterInternal();

            _registeredAniiMods.Remove(this);
            HarmonyInstance.UnpatchSelf();
            Registered = false;

            if (!silent)
                PrintUnloadInfo(reason);

            OnUnregister.Invoke();
            OnAniiModUnregistered.Invoke(this);
        }

        private void PrintLoadInfo()
        {
            AniiModLogger.WriteLine(ColorARGB.DarkGreen);
            
            AniiModLogger.PrintModName(ConsoleColor, AuthorConsoleColor, Info.Name, Info.Author, AdditionalCredits?.Credits, Info.Version, ID);
            AniiModLogger.MsgDirect(ColorARGB.DarkGray, $"Assembly: {Path.GetFileName(AniiModAssembly.Location)}");

            AniiModLogger.WriteLine(ColorARGB.DarkGreen);
        }

        private void PrintUnloadInfo(string reason)
        {
            AniiModLogger.WriteLine(ColorARGB.DarkRed);

            AniiModLogger.MsgDirect(ColorARGB.DarkGray, AniiModTypeName + " deinitialized:");
            AniiModLogger.PrintModName(ConsoleColor, AuthorConsoleColor, Info.Name, Info.Author, AdditionalCredits?.Credits, Info.Version, ID);

            if (!string.IsNullOrEmpty(reason))
            {
                AniiModLogger.MsgDirect(string.Empty);
                AniiModLogger.MsgDirect($"Reason: '{reason}'");
            }

            AniiModLogger.WriteLine(ColorARGB.DarkRed);
        }

        public static void ExecuteAll(LemonAction<AniiModBase> func, bool unregisterOnFail = false, string unregistrationReason = null)
        {
            ExecuteList(func, _registeredAniiMods, unregisterOnFail, unregistrationReason);
        }

        public static void ExecuteList<T>(LemonAction<T> func, List<T> aniimods, bool unregisterOnFail = false, string unregistrationReason = null) where T : AniiModBase
        {
            var failedAniiMods = (unregisterOnFail ? new List<T>() : null);

            LemonEnumerator<T> enumerator = new(aniimods.ToArray());
            while (enumerator.MoveNext())
            {
                var aniimod = enumerator.Current;
                if (!aniimod.Registered)
                    continue;

                try { func(aniimod); }
                catch (Exception ex)
                {
                    aniimod.LoggerInstance.Error(ex.ToString());
                    if (unregisterOnFail)
                        failedAniiMods.Add(aniimod);
                }
            }

            if (unregisterOnFail)
            {
                foreach (var m in failedAniiMods)
                    m.Unregister(unregistrationReason);
            }
        }

        public static void SendMessageAll(string name, params object[] arguments)
        {
            LemonEnumerator<AniiModBase> enumerator = new(_registeredAniiMods.ToArray());
            while (enumerator.MoveNext())
            {
                var aniimod = enumerator.Current;
                if (!aniimod.Registered)
                    continue;

                try { aniimod.SendMessage(name, arguments); }
                catch (Exception ex) { aniimod.LoggerInstance.Error(ex.ToString()); }
            }
        }

        public object SendMessage(string name, params object[] arguments)
        {
            var msg = Info.SystemType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (msg == null)
                return null;

            return msg.Invoke(msg.IsStatic ? null : this, arguments);
        }
        #endregion

        #region Obsolete Members

        [Obsolete]
        private Harmony.HarmonyInstance _OldHarmonyInstance;

        [Obsolete("Please use either the OnLateInitializeAniiMod callback, or the 'AniiModEvents::OnApplicationLateStart' event instead. This will be removed in a future update.", true)]
        public virtual void OnApplicationLateStart() { }

        [Obsolete("For mods, use OnInitializeAniiMod instead. For plugins, use OnPreModsLoaded instead. This will be removed in a future update.", true)]
        public virtual void OnApplicationStart() { }

        [Obsolete("Please use OnPreferencesSaved instead. This will be removed in a future update.", true)]
        public virtual void OnModSettingsApplied() { }

        [Obsolete("Please use HarmonyInstance instead. This will be removed in a future update.", true)]
#pragma warning disable IDE1006 // Naming Styles
        public Harmony.HarmonyInstance harmonyInstance { get { _OldHarmonyInstance ??= new Harmony.HarmonyInstance(HarmonyInstance.Id); return _OldHarmonyInstance; } }
#pragma warning restore IDE1006 // Naming Styles

        [Obsolete("Please use HarmonyInstance instead. This will be removed in a future update.", true)]
        public Harmony.HarmonyInstance Harmony { get { _OldHarmonyInstance ??= new Harmony.HarmonyInstance(HarmonyInstance.Id); return _OldHarmonyInstance; } }

        [Obsolete("Please use AniiModAssembly.Assembly instead. This will be removed in a future update.", true)]
        public Assembly Assembly => AniiModAssembly.Assembly;

        [Obsolete("Please use AniiModAssembly.HarmonyDontPatchAll instead. This will be removed in a future update.", true)]
        public bool HarmonyDontPatchAll => AniiModAssembly.HarmonyDontPatchAll;

        [Obsolete("Please use AniiModAssembly.Hash instead. This will be removed in a future update.", true)]
        public string Hash => AniiModAssembly.Hash;

        [Obsolete("Please use AniiModAssembly.Location instead. This will be removed in a future update.", true)]
        public string Location => AniiModAssembly.Location;

        #endregion


        public enum Incompatibility
        {
            MLVersion,
            MLBuild,
            Game,
            GameVersion,
            ProcessName,
            Domain,
            Platform
        }
    }
}
