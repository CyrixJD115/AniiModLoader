using AniiModLoader.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using AniiModLoader.Logging;

namespace AniiModLoader.AniiMods;

public static class AniiModFolderHandler
{
    private static bool _firstSpacer;
    private static List<string> _userLibDirs = [];
    private static List<string> _pluginDirs = [];
    private static List<string> _modDirs = [];
    private static List<string> _fullPathExclusions = [];

    public enum eNameExclusionType
    {
        EXACT_MATCH,
        STARTS_WITH,
        ENDS_WITH
    }
    private static List<LemonTuple<eNameExclusionType, string>> _nameExclusions = [
        new(eNameExclusionType.STARTS_WITH, "~"),
        new(eNameExclusionType.STARTS_WITH, "."),

        new(eNameExclusionType.EXACT_MATCH, "Broken"),
        new(eNameExclusionType.EXACT_MATCH, "Retired"),
        new(eNameExclusionType.EXACT_MATCH, "Disabled"),
    ];

    internal enum ScanType
    {
        UserLibs,
        Plugins,
        Mods
    }

    public static void AddNameExclusion(eNameExclusionType type, string param)
    {
        if (string.IsNullOrEmpty(param)
#if NET6_0_OR_GREATER
            || string.IsNullOrWhiteSpace(param)
#endif
            )
            throw new ArgumentNullException(nameof(param));

        _nameExclusions.Add(new(type, param));

        FixExclusions(ref _userLibDirs);
        FixExclusions(ref _pluginDirs);
        FixExclusions(ref _modDirs);
    }

    public static void AddFullPathExclusion(string path)
    {
        if (string.IsNullOrEmpty(path)
#if NET6_0_OR_GREATER
            || string.IsNullOrWhiteSpace(path)
#endif
            )
            throw new ArgumentNullException(nameof(path));

        _fullPathExclusions.Add(path);

        FixExclusions(ref _userLibDirs);
        FixExclusions(ref _pluginDirs);
        FixExclusions(ref _modDirs);
    }

    internal static void ScanForFolders()
    {
        // Add Base Directories to Start of List
        _userLibDirs.Add(AniiModEnvironment.UserLibsDirectory);
        _pluginDirs.Add(AniiModEnvironment.PluginsDirectory);
        _modDirs.Add(AniiModEnvironment.ModsDirectory);

        // Scan Base Folders
        FindSubFolders(ScanType.UserLibs, AniiModEnvironment.UserLibsDirectory, true, ref _userLibDirs, ref _pluginDirs, ref _modDirs);
        FindSubFolders(ScanType.Plugins, AniiModEnvironment.PluginsDirectory, true, ref _userLibDirs, ref _pluginDirs, ref _modDirs);
        FindSubFolders(ScanType.Mods, AniiModEnvironment.ModsDirectory, true, ref _userLibDirs, ref _pluginDirs, ref _modDirs);

        // Add Directories to Resolver
        foreach (string directory in _userLibDirs)
        {
            AniiModUtils.AddNativeDLLDirectory(directory);
            Resolver.AniiModAssemblyResolver.AddSearchDirectory(directory);
        }
        foreach (string directory in _pluginDirs)
            Resolver.AniiModAssemblyResolver.AddSearchDirectory(directory);
        foreach (string directory in _modDirs)
            Resolver.AniiModAssemblyResolver.AddSearchDirectory(directory);
    }

    internal static void LoadAniiMods(ScanType type)
    {
        LogStart(type);

        bool hasWroteLine = false;
        List<AniiModAssembly> aniimodAssemblies = new();

        if (type == ScanType.UserLibs)
            AniiModPreprocessor.LoadFolders(_userLibDirs, false, ref hasWroteLine, ref aniimodAssemblies);

        int count = aniimodAssemblies.Count;
        string typeName = "UserLib".MakePlural(count);

        if (type == ScanType.Plugins)
        {
            AniiModPreprocessor.LoadFolders(_pluginDirs, true, ref hasWroteLine, ref aniimodAssemblies);

            List<AniiModPlugin> pluginsLoaded = LoadAniiMods<AniiModPlugin>(aniimodAssemblies);
            if (hasWroteLine)
                AniiModLogger.WriteSpacer();
            AniiModBase.RegisterSorted(pluginsLoaded);

            count = AniiModPlugin.RegisteredAniiMods.Count;
            typeName = AniiModPlugin.TypeName.MakePlural(count);
        }

        if (type == ScanType.Mods)
        {
            AniiModPreprocessor.LoadFolders(_modDirs, true, ref hasWroteLine, ref aniimodAssemblies);

            List<AniiModMod> modsLoaded = LoadAniiMods<AniiModMod>(aniimodAssemblies);
            if (hasWroteLine)
                AniiModLogger.WriteSpacer();
            AniiModBase.RegisterSorted(modsLoaded);

            count = AniiModMod.RegisteredAniiMods.Count;
            typeName = AniiModMod.TypeName.MakePlural(count);
        }

        if (hasWroteLine)
            AniiModLogger.WriteLine(ColorARGB.Magenta);
        AniiModLogger.Msg($"{count} {typeName} loaded.");

        if (_firstSpacer || (type == ScanType.Mods))
            AniiModLogger.WriteSpacer();
        _firstSpacer = true;
    }

    private static void LogStart(ScanType type)
    {
        string typeName = Enum.GetName(typeof(ScanType), type);
        var loadingMsg = $"Loading {typeName}...";
        AniiModLogger.WriteSpacer();
        AniiModLogger.Msg(loadingMsg);
    }

    private static bool IsNameExcluded(LemonTuple<eNameExclusionType, string> tuple,
        string name)
        => tuple.Item1 switch
        {
            eNameExclusionType.EXACT_MATCH => (tuple.Item2 == name),
            eNameExclusionType.STARTS_WITH => name.StartsWith(tuple.Item2),
            eNameExclusionType.ENDS_WITH => name.EndsWith(tuple.Item2),
            _ => false,
        };

    private static bool IsExcluded(string path,
        string name)
    {
        foreach (var exclusion in _fullPathExclusions)
            if (exclusion == path)
                return true;

        foreach (var tuple in _nameExclusions)
            if (IsNameExcluded(tuple, name))
                return true;

        return false;
    }

    private static void FixExclusions(ref List<string> paths)
    {
        string[] localList = paths.ToArray();
        List<string> exclusions = new List<string>();

        foreach (var dir in localList)
        {
            // Check for Exclusion
            string directoryName = Path.GetFileName(dir);
            if (!IsExcluded(dir, directoryName))
                continue;
            exclusions.Add(dir);
        }

        foreach (var dir in localList)
            foreach (var exc in exclusions)
            {
                // Check for Exclusion
                if (!dir.Equals(exc)
                    && !dir.StartsWith(exc))
                    return;

                // Remove Path from Directory List
                paths.Remove(dir);

                // Remove Path from Resolver
                Resolver.AniiModAssemblyResolver.RemoveSearchDirectory(dir);
                // TO-DO: Remove Native Library Resolver
            }
    }

    private static List<T> LoadAniiMods<T>(List<AniiModAssembly> aniimodAssemblies)
        where T : AniiModTypeBase<T>
    {
        // Load AniiMods from Assembly
        foreach (var asm in aniimodAssemblies)
            asm.LoadAniiMods();

        List<T> loadedAniiMods = new();
        foreach (var asm in aniimodAssemblies)
        foreach (var m in asm.LoadedAniiMods)
        {
            // Validate Type
            if (m is T t)
            {
                loadedAniiMods.Add(t);
                continue;
            }

            // Log Failure
            AniiModLogger.Warning($"Failed to load AniiMod '{m.Info.Name}' from '{m.AniiModAssembly.Location}': The given AniiMod is a {m.AniiModTypeName} and cannot be loaded as a {AniiModTypeBase<T>.TypeName}. Make sure it's in the right folder.");
        }
        return loadedAniiMods;
    }

    private static void AddFolder(ScanType scanType,
        string path,
        string directoryName,
        bool require_manifest,
        ref List<string> userLibDirectories,
        ref List<string> pluginDirectories,
        ref List<string> modDirectories)
    {
        // Fix ScanType
        scanType = directoryName switch
        {
            // First check for Name Identifier
            "UserLibs" => ((scanType >= ScanType.UserLibs) ? ScanType.UserLibs : scanType),
            "Plugins" => ((scanType >= ScanType.Plugins) ? ScanType.Plugins : scanType),
            "Mods" => ((scanType >= ScanType.Mods) ? ScanType.Mods : scanType),

            // Last default to no change
            _ => scanType
        };

        // Get Directory List
        List<string> dirList = scanType switch
        {
            ScanType.UserLibs => userLibDirectories,
            ScanType.Plugins => pluginDirectories,
            ScanType.Mods => modDirectories,
            _ => throw new ArgumentOutOfRangeException(nameof(scanType), scanType, null)
        };

        // Add Path to List
        dirList.Add(path);

        // Find Sub Folders
        FindSubFolders(scanType, path, require_manifest, ref userLibDirectories, ref pluginDirectories, ref modDirectories);
    }

    private static void FindSubFolders(ScanType scanType,
        string path,
        bool require_manifest,
        ref List<string> userLibDirectories,
        ref List<string> pluginDirectories,
        ref List<string> modDirectories)
    {
        if (LoaderConfig.Current.Loader.DisableSubFolderLoad)
            return;

        // Get Directories
        string[] directories = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
        if (directories.Length <= 0)
            return;

        // Parse Directories
        foreach (var directoryPath in directories)
        {
            // Validate Path
            if (!Directory.Exists(directoryPath))
                continue;

            // Check for Exclusion
            string directoryName = Path.GetFileName(directoryPath);
            if (IsExcluded(directoryPath, directoryName))
                continue;

            // Check for manifest.json
            if (require_manifest 
                && !LoaderConfig.Current.Loader.DisableSubFolderManifest)
            {
                string manifestFilePath = Path.Combine(directoryPath, "manifest.json");
                if (!File.Exists(manifestFilePath))
                    continue;
            }

            // Add Folder
            AddFolder(scanType, directoryPath, directoryName, false, ref userLibDirectories, ref pluginDirectories, ref modDirectories);
        }
    }
}