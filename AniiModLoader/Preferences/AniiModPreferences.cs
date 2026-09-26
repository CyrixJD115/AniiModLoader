using System;
using System.Collections.Generic;
using System.IO;
using AniiModLoader.Preferences;
using AniiModLoader.Utils;
using Tomlet.Exceptions;

namespace AniiModLoader
{
    public static class AniiModPreferences
    {
        public static readonly List<AniiModPreferences_Category> Categories = new List<AniiModPreferences_Category>();
        public static readonly List<AniiModPreferences_ReflectiveCategory> ReflectiveCategories = new List<AniiModPreferences_ReflectiveCategory>();
        public static readonly TomlMapper Mapper = new TomlMapper();

        /// <summary>
        /// Occurs when a Preferences File has been loaded.
        /// <para>
        /// <see cref="string"/>: Path of the Preferences File.
        /// </para>
        /// </summary>
        public static readonly AniiModEvent<string> OnPreferencesLoaded = new AniiModEvent<string>();

        /// <summary>
        /// Occurs when a Preferences File has been saved.
        /// <para>
        /// <see cref="string"/>: Path of the Preferences File.
        /// </para>
        /// </summary>
        public static readonly AniiModEvent<string> OnPreferencesSaved = new AniiModEvent<string>();

        internal static List<Preferences.IO.File> PrefFiles = new List<Preferences.IO.File>();
        internal static Preferences.IO.File DefaultFile = null;

        static AniiModPreferences() => DefaultFile = new Preferences.IO.File(
            Compatibility.LoaderPaths.MigrateConfig(AniiModEnvironment.UserDataDirectory, "AniiModPreferences.cfg", "MelonPreferences.cfg"),
            Path.Combine(AniiModEnvironment.UserDataDirectory, "modprefs.ini"));

        public static void Load()
        {
            try
            {
                DefaultFile.LegacyLoad();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Error while Loading Legacy Preferences from {DefaultFile.LegacyFilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            try
            {
                DefaultFile.Load();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Error while Loading Preferences from {DefaultFile.FilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            if (PrefFiles.Count >= 0)
            {
                foreach (Preferences.IO.File file in PrefFiles)
                {
                    try
                    {
                        file.Load();
                    }
                    catch (Exception ex)
                    {
                        AniiModLogger.Error($"Error while Loading Preferences from {file.FilePath}: {ex}");
                        file.WasError = true;
                    }
                }
            }

            if (Categories.Count > 0)
            {
                foreach (AniiModPreferences_Category category in Categories)
                {
                    if (category.Entries.Count <= 0)
                        continue;
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    if (currentFile.WasError)
                        continue;
                    foreach (AniiModPreferences_Entry entry in category.Entries)
                        currentFile.SetupEntryFromRawValue(entry);
                }
            }

            if (ReflectiveCategories.Count > 0)
            {
                foreach (AniiModPreferences_ReflectiveCategory category in ReflectiveCategories)
                {
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    if (currentFile.WasError)
                        continue;
                    if (!(currentFile.TryGetCategoryTable(category.Identifier) is { } table))
                    {
                        category.LoadDefaults();
                        continue;
                    }
                    category.Load(table);
                }
            }

            AniiModLogger.Msg("Preferences Loaded!");
        }

        public static void Save()
        {
            if (Categories.Count > 0)
            {
                foreach (AniiModPreferences_Category category in Categories)
                {
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    foreach (AniiModPreferences_Entry entry in category.Entries)
                        if (!(entry.DontSaveDefault && entry.GetValueAsString() == entry.GetDefaultValueAsString()) && entry.GetValueAsString() != null)
                            currentFile.InsertIntoDocument(category.Identifier, entry.Identifier, entry.Save());
                }
            }
            if (ReflectiveCategories.Count > 0)
            {
                foreach (AniiModPreferences_ReflectiveCategory category in ReflectiveCategories)
                {
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    currentFile.document.PutValue(category.Identifier, category.Save());
                }
            }

            try
            {
                DefaultFile.Save();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Error while Saving Preferences to {DefaultFile.FilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            if (PrefFiles.Count >= 0)
            {
                foreach (Preferences.IO.File file in PrefFiles)
                {
                    try
                    {
                        file.Save();
                    }
                    catch (Exception ex)
                    {
                        AniiModLogger.Error($"Error while Saving Preferences to {file.FilePath}: {ex}");
                        file.WasError = true;
                    }
                }
            }

            AniiModLogger.Msg("Preferences Saved!");
        }

        public static AniiModPreferences_Category CreateCategory(string identifier) => CreateCategory(identifier, null, false);
        public static AniiModPreferences_Category CreateCategory(string identifier, string display_name = null) => CreateCategory(identifier, display_name, false);

        public static AniiModPreferences_Category CreateCategory(string identifier, string display_name = null, bool is_hidden = false, bool should_save = true)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling CreateCategory");
            if (display_name == null)
                display_name = identifier;
            AniiModPreferences_Category category = GetCategory(identifier);
            if (category != null)
                return category;
            return new AniiModPreferences_Category(identifier, display_name, is_hidden);
        }

        public static AniiModPreferences_ReflectiveCategory CreateCategory<T>(string identifier, string display_name = null) where T : new() => AniiModPreferences_ReflectiveCategory.Create<T>(identifier, display_name);

        [Obsolete]
        public static AniiModPreferences_Entry CreateEntry<T>(string category_identifier, string entry_identifier,
            T default_value, string display_name, bool is_hidden)
            => CreateEntry(category_identifier, entry_identifier, default_value, display_name, null, is_hidden, false, null);

        public static AniiModPreferences_Entry<T> CreateEntry<T>(string category_identifier, string entry_identifier, T default_value,
            string display_name = null, string description = null, bool is_hidden = false, bool dont_save_default = false,
            ValueValidator validator = null)
        {
            if (string.IsNullOrEmpty(category_identifier))
                throw new Exception("category_identifier is null or empty when calling CreateEntry");

            if (string.IsNullOrEmpty(entry_identifier))
                throw new Exception("entry_identifier is null or empty when calling CreateEntry");

            AniiModPreferences_Category category = GetCategory(entry_identifier);
            if (category == null)
                category = CreateCategory(category_identifier);

            return category.CreateEntry(entry_identifier, default_value, display_name, description, is_hidden, dont_save_default, validator);
        }

        public static AniiModPreferences_Category GetCategory(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling GetCategory");
            if (Categories.Count <= 0)
                return null;
            return Categories.Find(x => x.Identifier.Equals(identifier));
        }

        public static T GetCategory<T>(string identifier) where T : new()
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling GetCategory");
            if (ReflectiveCategories.Count <= 0)
                return default;
            AniiModPreferences_ReflectiveCategory category = ReflectiveCategories.Find(x => x.Identifier.Equals(identifier));
            if (category != null)
                return category.GetValue<T>();
            return default;
        }

        public static void SaveCategory<T>(string identifier, bool printmsg = true)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling GetCategory");
            if (ReflectiveCategories.Count <= 0)
                return;
            AniiModPreferences_ReflectiveCategory category = ReflectiveCategories.Find(x => x.Identifier.Equals(identifier));
            if (category != null)
                category.SaveToFile(printmsg);
        }

        public static AniiModPreferences_Entry GetEntry(string category_identifier, string entry_identifier) => GetCategory(category_identifier)?.GetEntry(entry_identifier);
        public static AniiModPreferences_Entry<T> GetEntry<T>(string category_identifier, string entry_identifier) => GetCategory(category_identifier)?.GetEntry<T>(entry_identifier);
        public static bool HasEntry(string category_identifier, string entry_identifier) => (GetEntry(category_identifier, entry_identifier) != null);

        public static void SetEntryValue<T>(string category_identifier, string entry_identifier, T value)
        {
            var entry = GetCategory(category_identifier)?.GetEntry<T>(entry_identifier);
            if (entry != null) entry.Value = value;
        }

        public static T GetEntryValue<T>(string category_identifier, string entry_identifier)
        {
            AniiModPreferences_Category cat = GetCategory(category_identifier);
            if (cat == null)
                return default;
            var entry = cat.GetEntry<T>(entry_identifier);
            if (entry == null)
                return default;
            return entry.Value;
        }

        internal static Preferences.IO.File GetPrefFileFromFilePath(string filepath)
        {
            if (PrefFiles.Count <= 0)
                return null;
            FileInfo filepathinfo = new FileInfo(filepath);
            foreach (Preferences.IO.File file in PrefFiles)
            {
                FileInfo filepathinfo2 = new FileInfo(file.FilePath);
                if (filepathinfo.FullName.Equals(filepathinfo2.FullName))
                    return file;
            }

            return null;
        }

        internal static bool IsFileInUse(Preferences.IO.File file)
        {
            if (Categories.Count <= 0)
                return false;
            if (file == null)
                file = DefaultFile;
            if (file == DefaultFile)
                return true;
            foreach (AniiModPreferences_Category category in Categories)
            {
                Preferences.IO.File currentFile = category.File;
                if (currentFile == null)
                    currentFile = DefaultFile;
                if (currentFile == file)
                    return true;
            }

            foreach (AniiModPreferences_ReflectiveCategory category in ReflectiveCategories)
            {
                Preferences.IO.File currentFile = category.File;
                if (currentFile == null)
                    currentFile = DefaultFile;
                if (currentFile == file)
                    return true;
            }

            return false;
        }

        internal static void LoadFileAndRefreshCategories(Preferences.IO.File file, bool printmsg = true)
        {
            try
            {
                file.Load();
            }
            catch (TomlUnescapedUnicodeControlCharException ex)
            {
                AniiModLogger.Error($"Error while Loading Preferences from {file.FilePath}: {ex}");
                return;
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Error while Loading Preferences from {file.FilePath}: {ex}");
                file.WasError = true;
                return;
            }

            if (Categories.Count > 0)
                foreach (AniiModPreferences_Category category in Categories)
                {
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    if ((currentFile != file) || (category.Entries.Count <= 0))
                        continue;
                    foreach (AniiModPreferences_Entry entry in category.Entries)
                        currentFile.SetupEntryFromRawValue(entry);
                }

            if (ReflectiveCategories.Count > 0)
                foreach (AniiModPreferences_ReflectiveCategory category in ReflectiveCategories)
                {
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    if (currentFile != file)
                        continue;
                    if (!(file.TryGetCategoryTable(category.Identifier) is { } table))
                    {
                        category.LoadDefaults();
                        continue;
                    }
                    category.Load(table);
                }

            if (printmsg)
                AniiModLogger.MsgDirect($"AniiModPreferences Loaded from {file.FilePath}");
        }

        public static void RemoveCategoryFromFile(string filePath, string categoryName)
        {
            Preferences.IO.File currentFile = GetPrefFileFromFilePath(filePath);
            if (currentFile == null)
                return;
            currentFile.RemoveCategoryFromDocument(categoryName);
        }

        internal static bool IsFilePathDefault(string filepath) => new FileInfo(filepath).FullName.Equals(new FileInfo(DefaultFile.FilePath).FullName);
    }
}