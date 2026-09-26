using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Semver;

namespace MelonLoader.Il2CppAssemblyGenerator.Packages
{
    internal class Cpp2IL : Models.ExecutablePackage
    {
        internal static SemVersion NetCoreMinVersion = SemVersion.Parse("2022.1.0-pre-release.18");
        internal SemVersion VersionSem;
        private string BaseFolder;
        internal bool UsedPrebaked { get; private set; }

        private static string ReleaseName =>
            MelonUtils.IsWindows ? "Windows" : MelonUtils.IsUnix ? "Linux" : "OSX";
		
        internal Cpp2IL()
        {
            Version = LoaderConfig.Current.UnityEngine.ForceIl2CppDumperVersion;
#if !DEBUG
            if (string.IsNullOrEmpty(Version) || Version.Equals("0.0.0.0"))
                Version = RemoteAPI.Info.ForceDumperVersion;
#endif
            if (string.IsNullOrEmpty(Version) || Version.Equals("0.0.0.0"))
                Version = $"2022.1.0-pre-release.21";
            VersionSem = SemVersion.Parse(Version);

            Name = nameof(Cpp2IL);
            
            var filename = Name;
#if WINDOWS
            filename += ".exe";
#endif

            BaseFolder = Path.Combine(Core.BasePath, Name);
            Directory.CreateDirectory(BaseFolder);

            FilePath =
                ExeFilePath =
                Destination =
                Path.Combine(BaseFolder, filename);

            OutputFolder = Path.Combine(BaseFolder, "cpp2il_out");

            URL = $"https://github.com/SamboyCoding/{Name}/releases/download/{Version}/{Name}-{Version}-{ReleaseName}";
#if WINDOWS
            URL += ".exe";
#endif
        }

        internal override bool ShouldSetup()
        {
            if (!File.Exists(ExeFilePath))
                return true;

            return string.IsNullOrEmpty(Config.Values.DumperVersion)
                || !Config.Values.DumperVersion.Equals(Version);
        }

        internal override void Cleanup() { }

        internal override void Save()
            => Save(ref Config.Values.DumperVersion);

#if OSX
        internal override bool OnProcess()
        {
            bool processed = base.OnProcess();
            if (!processed)
                return false;

            var process = Process.Start("chmod", $"+x \"{FilePath}\"");
            process?.WaitForExit();
            if (process?.ExitCode != 0)
            {
                Core.Logger.Error($"Failed to set the needed permissions on Cpp2IL, please run the following command " +
                                  $"and try again: chmod +x {FilePath}");
                return false;
            }
            return true;
        }
#endif

        internal override bool Execute()
        {
            // Aniimo: the game's custom sidecar metadata crashes stock
            // Cpp2IL's v29 attribute parsing. Prebaked DummyDlls (staged
            // by our tooling in PrebakedDummyDll/) bypass Cpp2IL entirely;
            // Il2CppInterop consumes the output folder the same way.
            var prebaked = Path.Combine(Core.BasePath, "PrebakedDummyDll");
            if (Directory.Exists(prebaked)
                && Directory.GetFiles(prebaked, "*.dll").Length > 0)
            {
                Core.Logger.Msg("Using Prebaked DummyDlls (bypassing Cpp2IL)...");
                Directory.CreateDirectory(OutputFolder);
                PrebakedAssemblySanitizer.Normalize(prebaked, OutputFolder, message => Core.Logger.Msg(message));
                UsedPrebaked = true;
                return true;
            }

            // Aniimo: global-metadata.dat is encrypted on disk; a shadow
            // game directory (symlinks to everything real except the
            // metadata, which points at the decrypted copy) lets Cpp2IL
            // see plain metadata without touching the game's file.
            var shadow = Path.Combine(Core.BasePath, "AniimoShadow");
            var shadowGameAssembly = Path.Combine(shadow, "GameAssembly.dll");
            var gamePath = File.Exists(shadowGameAssembly)
                ? "\"" + shadow + "\""
                : "\"" + Path.GetDirectoryName(Core.GameAssemblyPath) + "\"";
            return Execute([
                MelonDebug.IsEnabled() ? "--verbose" : string.Empty,

                "--game-path",
#if OSX
                "\"" + MelonUtils.GetPathAncestor(Core.GameAssemblyPath, 3) + "\"",
#else
                gamePath,
#endif

                "--exe-name",
                "\"" + Process.GetCurrentProcess().ProcessName + "\"",

                "--output-as",
                "dummydll",

                "--use-processor",
                "attributeanalyzer",
                "attributeinjector",
                LoaderConfig.Current.UnityEngine.EnableCpp2ILCallAnalyzer ? "callanalyzer" : string.Empty,
                LoaderConfig.Current.UnityEngine.EnableCpp2ILNativeMethodDetector ? "nativemethoddetector" : string.Empty,
                //"deobfmap",
                //"stablenamer",

            ], false, new Dictionary<string, string>() {
                {"NO_COLOR", "1"},
            });
        }
    }
}
