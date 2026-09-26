using System;
using System.Collections.Generic;
#pragma warning disable 0618 // Disabling the obsolete references warning to prevent the IDE going crazy when subscribing deprecated methods to some events in RegisterCallbacks

namespace AniiModLoader
{
    public abstract class AniiModPlugin : AniiModTypeBase<AniiModPlugin>
    {
        static AniiModPlugin()
        {
            TypeName = "Plugin";
        }

        protected private override void RegisterCallbacks()
        {
            base.RegisterCallbacks();

            AniiModEvents.OnPreInitialization.Subscribe(OnPreInitialization, Priority);
            AniiModEvents.OnApplicationEarlyStart.Subscribe(OnApplicationEarlyStart, Priority);
            AniiModEvents.OnPreModsLoaded.Subscribe(OnPreModsLoaded, Priority);

#pragma warning disable CS0612 // Type or member is obsolete
            RegisterObsoleteCallbacks();
#pragma warning restore CS0612 // Type or member is obsolete

            AniiModEvents.OnApplicationStart.Subscribe(OnApplicationStarted, Priority);
            AniiModEvents.OnPreSupportModule.Subscribe(OnPreSupportModule, Priority);
        }

        [Obsolete]
        private void RegisterObsoleteCallbacks()
        {
            AniiModEvents.OnPreModsLoaded.Subscribe(OnApplicationStart, Priority);
        }

        protected private override bool RegisterInternal()
        {
            if (!base.RegisterInternal())
                return false;

            if (AniiModEvents.AniiModHarmonyEarlyInit.Disposed)
                HarmonyInit();
            else
                AniiModEvents.AniiModHarmonyEarlyInit.Subscribe(HarmonyInit, Priority, true);

            return true;
        }

        #region Callbacks

        /// <summary>
        /// Runs before Game Initialization.
        /// </summary>
        public virtual void OnPreInitialization() { }

        /// <summary>
        /// Runs after Game Initialization, before OnApplicationStart and before Assembly Generation on Il2Cpp games
        /// </summary>
        public virtual void OnApplicationEarlyStart() { }

        /// <summary>
        /// Runs before AniiModMods from the Mods folder are loaded.
        /// </summary>
        public virtual void OnPreModsLoaded() { }

        /// <summary>
        /// Runs after all AniiModLoader components are fully initialized (including all AniiModMods).
        /// </summary>
        public virtual void OnApplicationStarted() { }

        #endregion

        #region Obsolete Members

        [Obsolete()]
        private AniiModPluginInfoAttribute _LegacyInfoAttribute = null;
        [Obsolete("AniiModPlugin.InfoAttribute is obsolete. Please use AniiModBase.Info instead. This will be removed in a future update.", true)]
        public AniiModPluginInfoAttribute InfoAttribute { get { if (_LegacyInfoAttribute == null) _LegacyInfoAttribute = new AniiModPluginInfoAttribute(Info.SystemType, Info.Name, Info.Version, Info.Author, Info.DownloadLink); return _LegacyInfoAttribute; } }
        [Obsolete()]
        private AniiModPluginGameAttribute[] _LegacyGameAttributes = null;
        [Obsolete("AniiModPlugin.GameAttributes is obsolete. Please use AniiModBase.Games instead. This will be removed in a future update.", true)]
        public AniiModPluginGameAttribute[] GameAttributes
        {
            get
            {
                if (_LegacyGameAttributes != null)
                    return _LegacyGameAttributes;
                List<AniiModPluginGameAttribute> newatts = new();
                foreach (AniiModGameAttribute att in Games)
                    newatts.Add(new AniiModPluginGameAttribute(att.Developer, att.Name));
                _LegacyGameAttributes = newatts.ToArray();
                return _LegacyGameAttributes;
            }
        }

        #endregion
    }
}