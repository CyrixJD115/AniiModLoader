using System;
using System.Collections.Generic;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModPrefs is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences instead. This will be removed in a future update.", true)]
    public class AniiModPrefs
    {
        [Obsolete("AniiModLoader.AniiModPrefs.RegisterCategory is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateCategory instead. This will be removed in a future update.", true)]
        public static void RegisterCategory(string name, string displayText) => AniiModPreferences.CreateCategory(name, displayText);
        [Obsolete("AniiModLoader.AniiModPrefs.RegisterString is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterString(string section, string name, string defaultValue, string displayText = null, bool hideFromList = false) => AniiModPreferences.CreateEntry(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.AniiModPrefs.RegisterBool is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterBool(string section, string name, bool defaultValue, string displayText = null, bool hideFromList = false) => AniiModPreferences.CreateEntry(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.AniiModPrefs.RegisterInt is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterInt(string section, string name, int defaultValue, string displayText = null, bool hideFromList = false) => AniiModPreferences.CreateEntry(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.AniiModPrefs.RegisterFloat is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.CreateEntry instead. This will be removed in a future update.", true)]
        public static void RegisterFloat(string section, string name, float defaultValue, string displayText = null, bool hideFromList = false) => AniiModPreferences.CreateEntry(section, name, defaultValue, displayText, hideFromList);
        [Obsolete("AniiModLoader.AniiModPrefs.HasKey is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.HasEntry instead. This will be removed in a future update.", true)]
        public static bool HasKey(string section, string name) => AniiModPreferences.HasEntry(section, name);
        [Obsolete("AniiModLoader.AniiModPrefs.GetPreferences is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.Categories instead. This will be removed in a future update.", true)]
        public static Dictionary<string, Dictionary<string, AniiModPreference>> GetPreferences()
        {
            Dictionary<string, Dictionary<string, AniiModPreference>> output = new Dictionary<string, Dictionary<string, AniiModPreference>>();
            if (AniiModPreferences.Categories.Count <= 0)
                return output;
            foreach (AniiModPreferences_Category category in AniiModPreferences.Categories)
            {
                Dictionary<string, AniiModPreference> newprefsdict = new Dictionary<string, AniiModPreference>();
                foreach (AniiModPreferences_Entry entry in category.Entries)
                {
                    Type reflectedType = entry.GetReflectedType();
                    if ((reflectedType != typeof(string))
                        && (reflectedType != typeof(bool))
                        && (reflectedType != typeof(int))
                        && (reflectedType != typeof(float))
                        && (reflectedType != typeof(double))
                        && (reflectedType != typeof(long)))
                        continue;
                    AniiModPreference newpref = new AniiModPreference(entry);
                    newprefsdict.Add(entry.Identifier, newpref);
                }
                output.Add(category.Identifier, newprefsdict);
            }
            return output;
        }
        [Obsolete("AniiModLoader.AniiModPrefs.GetCategoryDisplayName is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.GetCategoryDisplayName instead. This will be removed in a future update.", true)]
        public static string GetCategoryDisplayName(string key) => AniiModPreferences.GetCategory(key)?.DisplayName;
        [Obsolete("AniiModLoader.AniiModPrefs.SaveConfig is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.Save instead. This will be removed in a future update.", true)]
        public static void SaveConfig() => AniiModPreferences.Save();
        [Obsolete("AniiModLoader.AniiModPrefs.GetString is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.GetEntryString instead. This will be removed in a future update.", true)]
        public static string GetString(string section, string name)
        {
            AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
            if (category == null)
                return null;
            AniiModPreferences_Entry entry = category.GetEntry(name);
            if (entry == null)
                return null;
            return entry.GetValueAsString();
        }
        [Obsolete("AniiModLoader.AniiModPrefs.SetString is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.SetEntryString instead. This will be removed in a future update.", true)]
        public static void SetString(string section, string name, string value)
        {
            AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
            if (category == null)
                return;
            AniiModPreferences_Entry entry = category.GetEntry(name);
            if (entry == null)
                return;
            switch (entry)
            {
                case AniiModPreferences_Entry<string> stringEntry:
                    stringEntry.Value = value;
                    break;
                case AniiModPreferences_Entry<int> intEntry:
                    if (int.TryParse(value, out var parsedInt))
                        intEntry.Value = parsedInt;
                    break;
                case AniiModPreferences_Entry<float> floatEntry:
                    if (float.TryParse(value, out var parsedFloat))
                        floatEntry.Value = parsedFloat;
                    break;
                case AniiModPreferences_Entry<bool> boolEntry:
                    if (value.ToLower().StartsWith("true") || value.ToLower().StartsWith("false"))
                        boolEntry.Value = value.ToLower().StartsWith("true");
                    break;
            }
        }
        [Obsolete("AniiModLoader.AniiModPrefs.GetBool is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.GetEntryBool instead. This will be removed in a future update.", true)]
        public static bool GetBool(string section, string name) => AniiModPreferences.GetEntryValue<bool>(section, name);
        [Obsolete("AniiModLoader.AniiModPrefs.SetBool is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.SetEntryBool instead. This will be removed in a future update.", true)]
        public static void SetBool(string section, string name, bool value) => AniiModPreferences.SetEntryValue(section, name, value);
        [Obsolete("AniiModLoader.AniiModPrefs.GetInt is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.GetEntryInt instead. This will be removed in a future update.", true)]
        public static int GetInt(string section, string name) => AniiModPreferences.GetEntryValue<int>(section, name);
        [Obsolete("AniiModLoader.AniiModPrefs.SetInt is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.SetEntryInt instead. This will be removed in a future update.", true)]
        public static void SetInt(string section, string name, int value) => AniiModPreferences.SetEntryValue(section, name, value);
        [Obsolete("AniiModLoader.AniiModPrefs.GetFloat is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.GetEntryFloat instead. This will be removed in a future update.", true)]
        public static float GetFloat(string section, string name) => AniiModPreferences.GetEntryValue<float>(section, name);
        [Obsolete("AniiModLoader.AniiModPrefs.GetEntryFloat is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences.SetEntryFloat instead. This will be removed in a future update.", true)]
        public static void SetFloat(string section, string name, float value) => AniiModPreferences.SetEntryValue(section, name, value);
        [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreferenceType is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.TypeEnum instead. This will be removed in a future update.", true)]
        public enum AniiModPreferenceType
        {
            STRING,
            BOOL,
            INT,
            FLOAT
        }
        [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry instead. This will be removed in a future update.", true)]
        public class AniiModPreference
        {
            [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference.Value is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.GetValue instead. This will be removed in a future update.", true)]
            public string Value { get => GetString(Entry.Category.Identifier, Entry.Identifier); set => SetString(Entry.Category.Identifier, Entry.Identifier, value); }
            [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference.ValueEdited is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.GetValueEdited instead. This will be removed in a future update.", true)]
            public string ValueEdited { get => GetEditedString(Entry.Category.Identifier, Entry.Identifier); set => SetEditedString(Entry.Category.Identifier, Entry.Identifier, value); }
            [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference.Type is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.GetReflectedType instead. This will be removed in a future update.", true)]
            public AniiModPreferenceType Type
            {
                get
                {
                    if (Entry.GetReflectedType() == typeof(string))
                        return AniiModPreferenceType.STRING;
                    else if (Entry.GetReflectedType() == typeof(bool))
                        return AniiModPreferenceType.BOOL;
                    else if ((Entry.GetReflectedType() == typeof(float))
                        || (Entry.GetReflectedType() == typeof(double)))
                        return AniiModPreferenceType.FLOAT;
                    else if ((Entry.GetReflectedType() == typeof(int))
                        || (Entry.GetReflectedType() == typeof(long))
                        || (Entry.GetReflectedType() == typeof(byte)))
                        return AniiModPreferenceType.INT;
                    return (AniiModPreferenceType)4;
                }
            }
            [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference.Hidden is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.IsHidden instead. This will be removed in a future update.", true)]
            public bool Hidden { get => Entry.IsHidden; }
            [Obsolete("AniiModLoader.AniiModPrefs.AniiModPreference.DisplayText is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModPreferences_Entry.DisplayName instead. This will be removed in a future update.", true)]
            public string DisplayText { get => Entry.DisplayName; }

            internal AniiModPreference(AniiModPreferences_Entry entry) => Entry = entry;
            internal AniiModPreference(AniiModPreference pref) => Entry = pref.Entry;
            private AniiModPreferences_Entry Entry = null;
            private static string GetEditedString(string section, string name)
            {
                AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
                if (category == null)
                    return null;
                AniiModPreferences_Entry entry = category.GetEntry(name);
                if (entry == null)
                    return null;

                return entry.GetEditedValueAsString();
            }
            private static void SetEditedString(string section, string name, string value)
            {
                AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
                if (category == null)
                    return;
                AniiModPreferences_Entry entry = category.GetEntry(name);
                if (entry == null)
                    return;
                switch (entry)
                {
                    case AniiModPreferences_Entry<string> stringEntry:
                        stringEntry.EditedValue = value;
                        break;
                    case AniiModPreferences_Entry<int> intEntry:
                        if (int.TryParse(value, out var parsedInt))
                            intEntry.EditedValue = parsedInt;
                        break;
                    case AniiModPreferences_Entry<float> floatEntry:
                        if (float.TryParse(value, out var parsedFloat))
                            floatEntry.EditedValue = parsedFloat;
                        break;
                    case AniiModPreferences_Entry<bool> boolEntry:
                        if (value.ToLower().StartsWith("true") || value.ToLower().StartsWith("false"))
                            boolEntry.EditedValue = value.ToLower().StartsWith("true");
                        break;
                }
            }
        }
    }
}