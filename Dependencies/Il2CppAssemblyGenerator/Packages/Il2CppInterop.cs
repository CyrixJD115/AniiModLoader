using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using Microsoft.Extensions.Logging;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;

namespace MelonLoader.Il2CppAssemblyGenerator.Packages
{
    internal class Il2CppInterop : Models.ExecutablePackage
    {
        internal Il2CppInterop()
        {
            Version = typeof(Il2CppInteropGenerator).Assembly.CustomAttributes
                .Where(x => x.AttributeType.Name == "AssemblyInformationalVersionAttribute")
                .Select(x => x.ConstructorArguments[0].Value.ToString())
                .FirstOrDefault();

            Name = nameof(Il2CppInterop);
            Destination = Path.Combine(Core.BasePath, Name);
            OutputFolder = Path.Combine(Destination, "Il2CppAssemblies");
        }

        internal override bool ShouldSetup()
            => false;

        internal override bool Execute()
        {
            Core.Logger.Msg("Reading dumped assemblies for interop generation...");

            var resolver = new InteropResolver();
            var inputAssemblies = Directory.GetFiles(Core.cpp2il.OutputFolder)
                .Where(f => f.EndsWith(".dll"))
                .Select(f => ModuleDefinition.FromFile(f, new ModuleReaderParameters(Path.GetDirectoryName(f)) { ModuleResolver = resolver }))
                .Select(f => { resolver.Add(f); return f; })
                .Select(f => f.Assembly)
                .ToList();

            var opts = new GeneratorOptions()
            {
                GameAssemblyPath = Core.GameAssemblyPath,
                Source = inputAssemblies,
                OutputDir = OutputFolder,
                UnityBaseLibsDir = Core.unitydependencies.Destination,
                ObfuscatedNamesRegex = string.IsNullOrEmpty(Core.deobfuscationRegex.Regex) ? null : new Regex(Core.deobfuscationRegex.Regex),
                Parallel = true,
                // Protected Aniimo builds do not expose the metadata-init pattern
                // assumed by the optional xref cache. Keep normal generation intact.
                NoXrefCache = Core.cpp2il is Cpp2IL { UsedPrebaked: true },
                Il2CppPrefixMode = GeneratorOptions.PrefixMode.OptOut,
            };

            //Inform cecil of the unity base libs
            var trusted = (string)AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var allUnityDlls = string.Join(Path.PathSeparator, Directory.GetFiles(Core.unitydependencies.Destination, "*.dll", SearchOption.TopDirectoryOnly));
            // var allDumpedDlls = string.Join(Path.PathSeparator, Directory.GetFiles(Core.dumper.OutputFolder, "*.dll", SearchOption.TopDirectoryOnly));
            AppDomain.CurrentDomain.SetData("TRUSTED_PLATFORM_ASSEMBLIES", trusted + Path.PathSeparator + allUnityDlls);

            if (!string.IsNullOrEmpty(Core.deobfuscationMap.Version))
            {
                Core.Logger.Msg("Loading Deobfuscation Map...");
                opts.ReadRenameMap(Core.deobfuscationMap.Destination);
            }

            Core.Logger.Msg("Generating Interop Assemblies...");

#if !DEBUG
            try
#endif
            {
                Il2CppInteropGenerator.Create(opts)
                    .AddLogger(new InteropLogger())
                    .AddInteropAssemblyGenerator()
                    .Run();
            }
#if !DEBUG
            catch (Exception e)
            {
                Core.Logger.Error("Error Generating Interop Assemblies!", e);
                return false;
            }
#endif

            Core.Logger.Msg("Cleaning up...");
            AppDomain.CurrentDomain.SetData("TRUSTED_PLATFORM_ASSEMBLIES", trusted);
            //inputAssemblies.ForEach(a => a.Dispose());

            Core.Logger.Msg("Interop Generation Complete!");
            return true;
        }
    }
    
    internal class InteropLogger : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel is LogLevel.Debug or LogLevel.Trace)
            {
                MelonDebug.Msg(formatter(state, exception));
                return;
            }
            
            Core.Logger.Msg(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Debug or LogLevel.Trace => MelonDebug.IsEnabled(),
                _ => true
            };
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }

    internal class InteropResolver : INetModuleResolver
    {
        private readonly Dictionary<string, ModuleDefinition> _cache = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            _cache.Clear();
        }

        internal void Add(ModuleDefinition module)
        {
            // Aniimo: index the module under its metadata name AND its
            // bare assembly name (sans extension), because AsmResolver's
            // scope lookups arrive in both formats depending on path.
            _cache[module.Name] = module;
            var bare = Path.GetFileNameWithoutExtension(module.Name);
            _cache[bare] = module;
        }

        public ModuleDefinition Resolve(string name)
        {
            if (_cache.TryGetValue(name, out var hit))
                return hit;

            var bare = Path.GetFileNameWithoutExtension(name);
            if (_cache.TryGetValue(bare, out hit))
                return hit;

            Console.WriteLine($"[aniimo-resolver] MISS: '{name}' (known: {string.Join(", ", _cache.Keys.OrderBy(k => k).Take(8))}...)");
            return null;
        }
    }
}
