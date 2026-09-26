using System;
using AniiModLoader;

namespace IllusionPlugin
{
	public static class ModPrefs
	{
		public static string GetString(string section, string name, string defaultValue = "", bool autoSave = false)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<string> entry = category.GetEntry<string>(name);
			if (entry == null)
				entry = category.CreateEntry(name, defaultValue);
			return entry.Value;
		}

		public static int GetInt(string section, string name, int defaultValue = 0, bool autoSave = false)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<int> entry = category.GetEntry<int>(name);
			if (entry == null)
				entry = category.CreateEntry(name, defaultValue);
			return entry.Value;
		}

		public static float GetFloat(string section, string name, float defaultValue = 0f, bool autoSave = false)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<float> entry = category.GetEntry<float>(name);
			if (entry == null)
				entry = category.CreateEntry(name, defaultValue);
			return entry.Value;
		}

		public static bool GetBool(string section, string name, bool defaultValue = false, bool autoSave = false)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<bool> entry = category.GetEntry<bool>(name);
			if (entry == null)
				entry = category.CreateEntry(name, defaultValue);
			return entry.Value;
		}

		public static bool HasKey(string section, string name) => AniiModPreferences.HasEntry(section, name);

		public static void SetFloat(string section, string name, float value)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<float> entry = category.GetEntry<float>(name);
			if (entry == null)
				entry = category.CreateEntry(name, value);
			entry.Value = value;
		}

		public static void SetInt(string section, string name, int value)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<int> entry = category.GetEntry<int>(name);
			if (entry == null)
				entry = category.CreateEntry(name, value);
			entry.Value = value;
		}

		public static void SetString(string section, string name, string value)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<string> entry = category.GetEntry<string>(name);
			if (entry == null)
				entry = category.CreateEntry(name, value);
			entry.Value = value;
		}

		public static void SetBool(string section, string name, bool value)
		{
			AniiModPreferences_Category category = AniiModPreferences.GetCategory(section);
			if (category == null)
				category = AniiModPreferences.CreateCategory(section);
			AniiModPreferences_Entry<bool> entry = category.GetEntry<bool>(name);
			if (entry == null)
				entry = category.CreateEntry(name, value);
			entry.Value = value;
		}
	}
}