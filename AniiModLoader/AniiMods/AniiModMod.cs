using System;
using System.Collections.Generic;
#pragma warning disable 0618 // Disabling the obsolete references warning to prevent the IDE going crazy when subscribing deprecated methods to some events in RegisterCallbacks

namespace AniiModLoader
{
    public abstract class AniiModMod : AniiModTypeBase<AniiModMod>
    {
        static AniiModMod()
        {
            TypeName = "Mod";
        }

        protected private override bool RegisterInternal()
        {
            try
            {
                OnPreSupportModule();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Failed to register {AniiModTypeName} '{AniiModAssembly.Location}': AniiMod failed to initialize in the deprecated OnPreSupportModule callback!");
                AniiModLogger.Error(ex.ToString());
                return false;
            }

            if (!base.RegisterInternal())
                return false;

            if (AniiModEvents.AniiModHarmonyInit.Disposed)
                HarmonyInit();
            else
                AniiModEvents.AniiModHarmonyInit.Subscribe(HarmonyInit, Priority, true);

            return true;
        }

        protected private override void RegisterCallbacks()
        {
            base.RegisterCallbacks();

            AniiModEvents.OnSceneWasLoaded.Subscribe(OnSceneWasLoaded, Priority);
            AniiModEvents.OnSceneWasInitialized.Subscribe(OnSceneWasInitialized, Priority);
            AniiModEvents.OnSceneWasUnloaded.Subscribe(OnSceneWasUnloaded, Priority);

#pragma warning disable CS0612 // Type or member is obsolete
            RegisterObsoleteCallbacks();
#pragma warning restore CS0612 // Type or member is obsolete
        }

        [Obsolete]
        private void RegisterObsoleteCallbacks()
        {
            AniiModEvents.OnSceneWasLoaded.Subscribe((idx, name) => OnLevelWasLoaded(idx), Priority);
            AniiModEvents.OnSceneWasInitialized.Subscribe((idx, name) => OnLevelWasInitialized(idx), Priority);
            AniiModEvents.OnApplicationStart.Subscribe(OnApplicationStart, Priority);
        }

        #region Callbacks

        /// <summary>
        /// Runs when a new Scene is loaded.
        /// </summary>
        public virtual void OnSceneWasLoaded(int buildIndex, string sceneName) { }

        /// <summary>
        /// Runs once a Scene is initialized.
        /// </summary>
        public virtual void OnSceneWasInitialized(int buildIndex, string sceneName) { }

        /// <summary>
        /// Runs once a Scene unloads.
        /// </summary>
        public virtual void OnSceneWasUnloaded(int buildIndex, string sceneName) { }

        #endregion

        #region Obsolete Members
        [Obsolete("Override OnSceneWasLoaded instead. This will be removed in a future update.", true)]
        public virtual void OnLevelWasLoaded(int level) { }
        [Obsolete("Override OnSceneWasInitialized instead. This will be removed in a future update.", true)]
        public virtual void OnLevelWasInitialized(int level) { }

        [Obsolete()]
        private AniiModModInfoAttribute _LegacyInfoAttribute = null;
        [Obsolete("Use AniiModBase.Info instead. This will be removed in a future update.", true)]
        public AniiModModInfoAttribute InfoAttribute { get { if (_LegacyInfoAttribute == null) _LegacyInfoAttribute = new AniiModModInfoAttribute(Info.SystemType, Info.Name, Info.Version, Info.Author, Info.DownloadLink); return _LegacyInfoAttribute; } }
        [Obsolete()]
        private AniiModModGameAttribute[] _LegacyGameAttributes = null;
        [Obsolete("Use AniiModBase.Games instead. This will be removed in a future update.", true)]
        public AniiModModGameAttribute[] GameAttributes { get {
                if (_LegacyGameAttributes != null)
                    return _LegacyGameAttributes;
                List<AniiModModGameAttribute> newatts = new();
                foreach (AniiModGameAttribute att in Games)
                    newatts.Add(new AniiModModGameAttribute(att.Developer, att.Name));
                _LegacyGameAttributes = newatts.ToArray();
                return _LegacyGameAttributes;
            } }

        #endregion
    }
}