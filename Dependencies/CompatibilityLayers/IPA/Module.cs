using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IllusionPlugin;
using IllusionInjector;
using AniiModLoader.Modules;
using AniiModLoader.Resolver;

[assembly: AniiModLoader.PatchShield]

namespace AniiModLoader.CompatibilityLayers
{
    internal class IPA_Module : AniiModModule
    {
        public override void OnInitialize()
        {
			// To-Do:
			// Detect if IPA is already Installed
			// Point AssemblyResolveInfo to already installed IPA Assembly
			// Point GetResolverFromAssembly to Dummy AniiModCompatibilityLayer.Resolver

			string[] assembly_list =
			{
				"IllusionPlugin",
				"IllusionInjector",
			};
			Assembly base_assembly = typeof(IPA_Module).Assembly;
			foreach (string assemblyName in assembly_list)
                AniiModAssemblyResolver.GetAssemblyResolveInfo(assemblyName).Override = base_assembly;

			AniiModAssembly.CustomAniiModResolvers += Resolve;
		}

        private ResolvedAniiMods Resolve(Assembly asm)
		{
			IEnumerable<Type> pluginTypes = asm.GetValidTypes(x =>
			{
				Type[] interfaces = x.GetInterfaces();
				return (interfaces != null) && interfaces.Any() && interfaces.Contains(typeof(IPlugin)); // To-Do: Change to Type Reflection based on Setup
			});
			if ((pluginTypes == null) || !pluginTypes.Any())
				return new ResolvedAniiMods(null, null);

			var aniimods = new List<AniiModBase>();
			var rotten = new List<RottenAniiMod>();
			foreach (var t in pluginTypes)
            {
				var mel = LoadPlugin(asm, t, out RottenAniiMod rm);
				if (mel != null)
					aniimods.Add(mel);
				else
					rotten.Add(rm);
            }
			return new ResolvedAniiMods(aniimods.ToArray(), rotten.ToArray());
		}

		private AniiModBase LoadPlugin(Assembly asm, Type pluginType, out RottenAniiMod rottenAniiMod)
		{
			rottenAniiMod = null;
			IPlugin pluginInstance;
			try
			{ pluginInstance = Activator.CreateInstance(pluginType) as IPlugin; }
            catch (Exception ex)
            {
				rottenAniiMod = new RottenAniiMod(pluginType, "Failed to create a new instance of the IPA Plugin.", ex);
				return null;
            }

			AniiModProcessAttribute[] processAttrs = null;
			if (pluginInstance is IEnhancedPlugin enPl)
				processAttrs = enPl.Filter?.Select(x => new AniiModProcessAttribute(x)).ToArray();

			string pluginName = pluginInstance.Name;
			if (string.IsNullOrEmpty(pluginName))
				pluginName = pluginType.FullName;

			string plugin_version = pluginInstance.Version;
			if (string.IsNullOrEmpty(plugin_version))
				plugin_version = asm.GetName().Version.ToString();
			if (string.IsNullOrEmpty(plugin_version) || plugin_version.Equals("0.0.0.0"))
				plugin_version = "1.0.0.0";

			var aniimod = AniiModBase.CreateWrapper<IPAPluginWrapper>(pluginName, null, plugin_version, processes: processAttrs);

			aniimod.pluginInstance = pluginInstance;
			PluginManager._Plugins.Add(pluginInstance);
			return aniimod;
		}
	}
}