using System;
using System.Reflection;

namespace AniiModLoader.MonoInternals
{
    [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver instead. This will be removed in a future update.", true)]
    public static class MonoResolveManager
    {
        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.AddSearchDirectory is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.AddSearchDirectory instead. This will be removed in a future update.", true)]
        public static void AddSearchDirectory(string path, int priority = 0)
            => Resolver.SearchDirectoryManager.Add(path, priority);

        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.RemoveSearchDirectory is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.RemoveSearchDirectory instead. This will be removed in a future update.", true)]
        public static void RemoveSearchDirectory(string path)
            => Resolver.SearchDirectoryManager.Remove(path);

        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.OnAssemblyLoadHandler is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.dOnAssemblyLoad instead. This will be removed in a future update.", true)]
        public delegate void OnAssemblyLoadHandler(Assembly assembly);
        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.OnAssemblyLoad is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.OnAssemblyLoad instead. This will be removed in a future update.", true)]
        public static event OnAssemblyLoadHandler OnAssemblyLoad;
        internal static void SafeInvoke_OnAssemblyLoad(Assembly assembly)
            => OnAssemblyLoad?.Invoke(assembly);

        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.OnAssemblyResolveHandler is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.dOnAssemblyResolve instead. This will be removed in a future update.", true)]
        public delegate Assembly OnAssemblyResolveHandler(string name, Version version);
        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.OnAssemblyLoad is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.OnAssemblyLoad instead. This will be removed in a future update.", true)]
        public static event OnAssemblyResolveHandler OnAssemblyResolve;
        internal static Assembly SafeInvoke_OnAssemblyResolve(string name, Version version)
            => OnAssemblyResolve?.Invoke(name, version);

        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.GetAssemblyResolveInfo is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.GetAssemblyResolveInfo instead. This will be removed in a future update.", true)]
        public static AssemblyResolveInfo GetAssemblyResolveInfo(string name)
            => (AssemblyResolveInfo)Resolver.AssemblyManager.GetInfo(name);

        [Obsolete("AniiModLoader.MonoInternals.MonoResolveManager.LoadInfoFromAssembly is Only Here for Compatibility Reasons. Please use AniiModLoader.Resolver.AniiModAssemblyResolver.LoadInfoFromAssembly instead. This will be removed in a future update.", true)]
        public static void LoadInfoFromAssembly(Assembly assembly)
            => Resolver.AssemblyManager.LoadInfo(assembly);
    }
}
