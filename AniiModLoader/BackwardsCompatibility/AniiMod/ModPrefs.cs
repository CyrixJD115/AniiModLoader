using System;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable 0108

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.ModPrefs is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences instead. This will be removed in a future update.", true)]
    public class ModPrefs : AniiModPrefs
    {
        [Obsolete("AniiModLoader.ModPrefs.GetPrefs is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences instead. This will be removed in a future update.", true)]
        public static Dictionary<string, Dictionary<string, PrefDesc>> GetPrefs()
        {
            Dictionary<string, Dictionary<string, PrefDesc>> output = new Dictionary<string, Dictionary<string, PrefDesc>>();
            Dictionary<string, Dictionary<string, AniiModPreference>> prefs = GetPreferences();
            for (int i = 0; i < prefs.Values.Count; i++)
            {
                Dictionary<string, AniiModPreference> prefsdict = prefs.Values.ElementAt(i);
                Dictionary<string, PrefDesc> newprefsdict = new Dictionary<string, PrefDesc>();
                for (int j = 0; j < prefsdict.Values.Count; j++)
                {
                    AniiModPreference pref = prefsdict.Values.ElementAt(j);
                    PrefDesc newpref = new PrefDesc(pref);
                    newpref.ValueEdited = pref.ValueEdited;
                    newprefsdict.Add(prefsdict.Keys.ElementAt(j), newpref);
                }
                output.Add(prefs.Keys.ElementAt(i), newprefsdict);
            }
            return output;
        }
        [Obsolete("AniiModLoader.ModPrefs.RegisterPrefString is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterPrefString(string section, string name, string defaultValue, string displayText = null, bool hideFromList = false) => RegisterString(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.ModPrefs.RegisterPrefBool is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterPrefBool(string section, string name, bool defaultValue, string displayText = null, bool hideFromList = false) => RegisterBool(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.ModPrefs.RegisterPrefInt is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterPrefInt(string section, string name, int defaultValue, string displayText = null, bool hideFromList = false) => RegisterInt(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.ModPrefs.RegisterPrefFloat is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterPrefFloat(string section, string name, float defaultValue, string displayText = null, bool hideFromList = false) => RegisterFloat(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.ModPrefs.PrefType is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.TypeEnum instead. This will be removed in a future update.", true)]
        public enum PrefType
        {
            STRING,
            BOOL,
            INT,
            FLOAT
        }
        [Obsolete("AniiModLoader.ModPrefs.PrefDesc is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry instead. This will be removed in a future update.", true)]
        public class PrefDesc : AniiModPreference
        {
            [Obsolete("AniiModLoader.ModPrefs.PrefDesc.Type is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.Type instead. This will be removed in a future update.", true)]
            public PrefType Type { get => (PrefType)base.Type; }
            [Obsolete("AniiModLoader.ModPrefs.PrefDesc is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry instead. This will be removed in a future update.", true)]
            public PrefDesc(AniiModPreferences_Entry entry) : base(entry) { }
            [Obsolete("AniiModLoader.ModPrefs.PrefDesc is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry instead. This will be removed in a future update.", true)]
            public PrefDesc(AniiModPreference pref) : base(pref) { }
        }
    }
}