using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AniiModLoader.Modules;
using ModHelper;
using AniiModLoader.Resolver;

[assembly: AniiModLoader.PatchShield]

namespace AniiModLoader.CompatibilityLayers
{
    internal class Muse_Dash_Mono_Module : AniiModModule
    {
        public override void OnInitialize()
        {
            // To-Do:
            // Detect if MuseDashModLoader is already Installed
            // Point AssemblyResolveInfo to already installed MuseDashModLoader Assembly
            // Inject Custom Resolver

            string[] assembly_list =
            {
                "ModHelper",
                "ModLoader",
            };
            Assembly base_assembly = typeof(Muse_Dash_Mono_Module).Assembly;
            foreach (string assemblyName in assembly_list)
                AniiModAssemblyResolver.GetAssemblyResolveInfo(assemblyName).Override = base_assembly;

            AniiModAssembly.CustomAniiModResolvers += Resolve;
        }

        private ResolvedAniiMods Resolve(Assembly asm)
        {
            IEnumerable<Type> modTypes = asm.GetValidTypes(x =>
            {
                Type[] interfaces = x.GetInterfaces();
                return (interfaces != null) && interfaces.Any() && interfaces.Contains(typeof(IMod));  // To-Do: Change to Type Reflection based on Setup
            });
            if ((modTypes == null) || !modTypes.Any())
                return new ResolvedAniiMods(null, null);

            var aniimods = new List<AniiModBase>();
            var rotten = new List<RottenAniiMod>();
            foreach (var t in modTypes)
            {
                var mel = LoadMod(asm, t, out RottenAniiMod rm);
                if (mel != null)
                    aniimods.Add(mel);
                else
                    rotten.Add(rm);
            }
            return new ResolvedAniiMods(aniimods.ToArray(), rotten.ToArray());
        }

        private AniiModBase LoadMod(Assembly asm, Type modType, out RottenAniiMod rottenAniiMod)
        {
            rottenAniiMod = null;

            IMod modInstance;
            try { modInstance = Activator.CreateInstance(modType) as IMod; }
            catch (Exception ex)
            {
                rottenAniiMod = new RottenAniiMod(modType, "Failed to create an instance of the MMDL Mod.", ex);
                return null;
            }

            var modName = modInstance.Name;

            if (string.IsNullOrEmpty(modName))
                modName = modType.FullName;

            var modVersion = asm.GetName().Version.ToString();
            if (string.IsNullOrEmpty(modVersion) || modVersion.Equals("0.0.0.0"))
                modVersion = "1.0.0.0";

            var aniimod = AniiModBase.CreateWrapper<MuseDashModWrapper>(modName, null, modVersion);
            aniimod.modInstance = modInstance;
            ModLoader.ModLoader.mods.Add(modInstance);
            ModLoader.ModLoader.LoadDependency(asm);
            return aniimod;
        }
    }
}