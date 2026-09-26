using System;
using Tomlet;
using Tomlet.Exceptions;
using Tomlet.Models;

namespace AniiModLoader.Preferences
{
    public class AniiModPreferences_ReflectiveCategory
    {
        private Type SystemType;
        private object value;
        internal IO.File File = null;
        
        public string Identifier { get;  internal set; }
        public string DisplayName { get; internal set; }

        internal static AniiModPreferences_ReflectiveCategory Create<T>(string categoryName, string displayName) => new AniiModPreferences_ReflectiveCategory(typeof(T), categoryName, displayName);
        
        private AniiModPreferences_ReflectiveCategory(Type type, string categoryName, string displayName)
        {
            SystemType = type;
            Identifier = categoryName;
            DisplayName = displayName;

            IO.File currentFile = File;
            if (currentFile == null)
                currentFile = AniiModPreferences.DefaultFile;
            if (!(currentFile.TryGetCategoryTable(Identifier) is { } table))
                LoadDefaults();
            else
                Load(table);

            AniiModPreferences.ReflectiveCategories.Add(this);
        }

        internal void LoadDefaults() => value = Activator.CreateInstance(SystemType);

        internal void Load(TomlValue tomlValue)
        {
            try { value = TomletMain.To(SystemType, tomlValue); }
            catch (TomlTypeMismatchException)
            {
                return;
            }
            catch (TomlNoSuchValueException)
            {
                return;
            }
            catch (TomlEnumParseException)
            {
                return;
            }
        }

        internal TomlValue Save()
        {
            if(value == null)
                LoadDefaults();
            
            return TomletMain.ValueFrom(SystemType, value);
        }

        public T GetValue<T>() where T : new()
        {
            if (typeof(T) != SystemType)
                return default;
            if (value == null)
                LoadDefaults();
            return (T) value;
        }
        
        public void SetFilePath(string filepath, bool autoload = true, bool printmsg = true)
        {
            if (File != null)
            {
                IO.File oldfile = File;
                File = null;
                if (!AniiModPreferences.IsFileInUse(oldfile))
                {
                    oldfile.FileWatcher.Destroy();
                    AniiModPreferences.PrefFiles.Remove(oldfile);
                }
            }
            if (!string.IsNullOrEmpty(filepath) && !AniiModPreferences.IsFilePathDefault(filepath))
            {
                File = AniiModPreferences.GetPrefFileFromFilePath(filepath);
                if (File == null)
                {
                    File = new IO.File(filepath);
                    AniiModPreferences.PrefFiles.Add(File);
                }
            }
            if (autoload)
                AniiModPreferences.LoadFileAndRefreshCategories(File, printmsg);
        }

        public void ResetFilePath()
        {
            if (File == null)
                return;
            IO.File oldfile = File;
            File = null;
            if (!AniiModPreferences.IsFileInUse(oldfile))
            {
                oldfile.FileWatcher.Destroy();
                AniiModPreferences.PrefFiles.Remove(oldfile);
            }
            AniiModPreferences.LoadFileAndRefreshCategories(AniiModPreferences.DefaultFile);
        }
        
        public void SaveToFile(bool printmsg = true)
        {
            IO.File currentfile = File;
            if (currentfile == null)
                currentfile = AniiModPreferences.DefaultFile;

            currentfile.document.PutValue(Identifier, Save());
            try
            {
                currentfile.Save();
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Error while Saving Preferences to {currentfile.FilePath}: {ex}");
                currentfile.WasError = true;
            }
            if (printmsg)
                AniiModLogger.Msg($"AniiModPreferences Saved to {currentfile.FilePath}");
        }

        public void LoadFromFile(bool printmsg = true)
        {
            IO.File currentfile = File;
            if (currentfile == null)
                currentfile = AniiModPreferences.DefaultFile;
            AniiModPreferences.LoadFileAndRefreshCategories(currentfile, printmsg);
        }

        public void DestroyFileWatcher() => File?.FileWatcher.Destroy();
    }
}