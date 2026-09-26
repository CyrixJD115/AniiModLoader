using Il2CppInterop.Generator.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using Microsoft.Extensions.Logging;

if (args.Length == 3 && args[0] == "--sanitize")
{
    MelonLoader.Il2CppAssemblyGenerator.Packages.PrebakedAssemblySanitizer.Normalize(args[1], args[2], Console.WriteLine);
    return;
}
if (args.Length < 3) throw new ArgumentException("Usage: <input> <GameAssembly> <output> [--scan] [--parallel] [--no-xref], or --sanitize <input> <output>");
var input = Path.GetFullPath(args[0]); // directory containing dumped assemblies
var gameAssembly = args[1]; // path to GameAssembly.dll
var output = args[2];

var resolver = new InteropResolver();
var inputAssemblies = Directory.GetFiles(input, "*.dll")
    .Where(f => f.EndsWith(".dll"))
    .Select(f => ModuleDefinition.FromFile(f, new ModuleReaderParameters(
        Path.GetDirectoryName(f))
    { ModuleResolver = resolver }))
    .Select(f => { resolver.Add(f); return f; })
    .Select(f => f.Assembly ?? throw new InvalidDataException($"Module {f.Name} has no assembly"))
    .ToList();

if (args.Contains("--inventory"))
{
    foreach (var assembly in inputAssemblies.OrderBy(a => a.Name?.ToString(), StringComparer.Ordinal))
        foreach (var type in assembly.ManifestModule!.GetAllTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            Console.WriteLine($"TYPE {assembly.Name}::{type.FullName} : {type.BaseType?.FullName}");
            foreach (var field in type.Fields.OrderBy(f => f.FullName, StringComparer.Ordinal)) Console.WriteLine($"FIELD {field.FullName}");
            foreach (var method in type.Methods.OrderBy(m => m.FullName, StringComparer.Ordinal)) Console.WriteLine($"METHOD {method.FullName}");
        }
    return;
}

if (args.Contains("--scan"))
{
    int unresolved = 0;
    int invalidSignatures = 0;
    foreach (var asm in inputAssemblies) foreach (var type in asm!.ManifestModule!.GetAllTypes()) foreach (var method in type.Methods)
            {
                var signatures = method.Signature!.ParameterTypes.Prepend(method.Signature.ReturnType);
                foreach (var sig in signatures) try { sig.GetUnmangledName(type, method); } catch (Exception e) { invalidSignatures++; Console.WriteLine($"BAD SIGNATURE {type.FullName}::{method.Name} type-gp={type.GenericParameters.Count} method-gp={method.GenericParameters.Count} signature={sig.FullName}: {e.Message}"); }
            }

    foreach (var asm in inputAssemblies) foreach (var type in asm!.ManifestModule!.GetAllTypes())
        {
            if (type.BaseType == null) continue;
            try { if (type.BaseType.Resolve() == null) { Console.WriteLine($"UNRESOLVED {asm.Name} :: {type.FullName} => {type.BaseType.FullName} [{type.BaseType.Scope}]"); unresolved++; } }
            catch (Exception e) { Console.WriteLine($"ERROR {type.FullName} => {type.BaseType}: {e.Message}"); unresolved++; }
        }
    Console.WriteLine($"Unresolved bases: {unresolved}; invalid signatures: {invalidSignatures}");
    Environment.ExitCode = unresolved + invalidSignatures == 0 ? 0 : 2;
    return;
}

var opts = new GeneratorOptions
{
    GameAssemblyPath = gameAssembly,
    Source = inputAssemblies,
    OutputDir = output,
    Parallel = args.Contains("--parallel"),
    NoXrefCache = args.Contains("--no-xref"),
    Il2CppPrefixMode = GeneratorOptions.PrefixMode.OptOut,
};

Il2CppInteropGenerator.Create(opts)
    .AddLogger(new ConsoleLogger())
    .AddInteropAssemblyGenerator()
    .Run();
Console.WriteLine("GENERATION OK");

internal class InteropResolver : INetModuleResolver
{
    private readonly Dictionary<string, ModuleDefinition> _cache = new();
    public void Dispose() => _cache.Clear();
    internal void Add(ModuleDefinition module) => _cache[module.Name?.ToString() ?? throw new InvalidDataException("Module name missing")] = module;
    public ModuleDefinition? Resolve(string name) => _cache.GetValueOrDefault(name);
}

internal class ConsoleLogger : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => NoopScope.Instance;
    private sealed class NoopScope : IDisposable { internal static readonly NoopScope Instance = new(); public void Dispose() { } }
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Console.WriteLine($"[{logLevel}] {formatter(state, exception)}\n{exception}");
}
