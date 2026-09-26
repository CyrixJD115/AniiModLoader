using System;
using System.IO;
using System.Collections.Generic;
using Mono.Cecil;

namespace MelonLoader.Il2CppAssemblyGenerator.Packages
{
    // Repairs only the working copy consumed by Il2CppInterop. Never write to
    // PrebakedDummyDll or to the installed game metadata/binary.
    internal static class PrebakedAssemblySanitizer
    {
        internal static void Normalize(string input, string output, Action<string> log)
        {
            input = Path.GetFullPath(input);
            output = Path.GetFullPath(output);
            if (input == output) throw new ArgumentException("Input and output must differ.");
            Directory.CreateDirectory(output);
            foreach (var file in Directory.GetFiles(input))
            {
                if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(file) != "__Generated") continue;
                using var module = ModuleDefinition.ReadModule(file);
                foreach (var type in AllTypes(module.Types))
                {
                    // A stripped anonymous type still declares its arity in its metadata name.
                    if (type.Name.StartsWith("<>f__AnonymousType") && type.Name.EndsWith("`1") && type.GenericParameters.Count == 0)
                    {
                        type.GenericParameters.Add(new GenericParameter("T0", type));
                        log($"Restored declared generic arity: {module.Name}::{type.FullName}");
                    }
                    foreach (var method in type.Methods)
                    {
                        method.ReturnType = Fix(method.ReturnType, method, log);
                        foreach (var parameter in method.Parameters) parameter.ParameterType = Fix(parameter.ParameterType, method, log);
                    }
                }
                var name = Path.GetFileName(file); if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name += ".dll";
                module.Write(Path.Combine(output, name));
            }
        }
        static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types) { yield return type; foreach (var nested in AllTypes(type.NestedTypes)) yield return nested; }
        }
        static TypeReference Fix(TypeReference type, MethodDefinition method, Action<string> log)
        {
            if (type is GenericParameter gp && gp.Type == GenericParameterType.Type && method.DeclaringType.GenericParameters.Count == 0 && gp.Position >= 0 && gp.Position < method.GenericParameters.Count)
            {
                log($"Generic owner: {method.FullName}: !{gp.Position} -> !!{gp.Position}");
                return method.GenericParameters[gp.Position];
            }
            if (type is GenericInstanceType gi) { for (int i = 0; i < gi.GenericArguments.Count; i++) gi.GenericArguments[i] = Fix(gi.GenericArguments[i], method, log); return gi; }
            if (type is ByReferenceType br) return new ByReferenceType(Fix(br.ElementType, method, log));
            if (type is PointerType pt) return new PointerType(Fix(pt.ElementType, method, log));
            if (type is ArrayType ar) { var result = new ArrayType(Fix(ar.ElementType, method, log), ar.Rank); for (int i = 0; i < ar.Dimensions.Count; i++) result.Dimensions[i] = ar.Dimensions[i]; return result; }
            return type;
        }
    }
}
