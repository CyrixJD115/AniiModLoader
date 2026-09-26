using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AniiModLoader.Modules;

[assembly: AniiModLoader.PatchShield]

namespace AniiModLoader.CompatibilityLayers
{
    internal class Demeo_Module : AniiModModule
    {
        private Dictionary<AniiModBase, object> ModInformation = new Dictionary<AniiModBase, object>();
        private IList ModInfoList;

        private Type modInfoType;
        private FieldInfo name_field;
        private FieldInfo version_field;
        private FieldInfo author_field;
        private FieldInfo description_field;
        private FieldInfo isNetworkCompatible_field;

        public override void OnInitialize()
        {
            AniiModEvents.OnApplicationStart.Subscribe(OnPreAppStart, int.MaxValue);
            AniiModBase.OnAniiModRegistered.Subscribe(ParseAniiMod, int.MaxValue);
            AniiModBase.OnAniiModUnregistered.Subscribe(OnUnregister, int.MaxValue);
        }

        private void OnPreAppStart()
        {
            try
            {
                Assembly assembly = Assembly.Load("Assembly-CSharp");
                Type moddingApi = assembly.GetType("Boardgame.Modding.ModdingAPI");

                modInfoType = moddingApi.GetNestedType("ModInformation");
                name_field = modInfoType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
                version_field = modInfoType.GetField("version", BindingFlags.Public | BindingFlags.Instance);
                author_field = modInfoType.GetField("author", BindingFlags.Public | BindingFlags.Instance);
                description_field = modInfoType.GetField("description", BindingFlags.Public | BindingFlags.Instance);
                isNetworkCompatible_field = modInfoType.GetField("isNetworkCompatible", BindingFlags.Public | BindingFlags.Instance);

                FieldInfo externalModsField = moddingApi.GetField("ExternallyInstalledMods", BindingFlags.Public | BindingFlags.Static);
                if (externalModsField.GetValue(null) == null)
                {
                    var listType = typeof(List<>);
                    var constructedListType = listType.MakeGenericType(modInfoType);
                    ModInfoList = (IList)Activator.CreateInstance(constructedListType);
                    externalModsField.SetValue(null, ModInfoList);
                }

                foreach (var m in AniiModPlugin.RegisteredAniiMods)
                {
                    try
                    {
                        ParseAniiMod(m);
                    }
                    catch (Exception ex)
                    {
                        AniiModLogger.Error($"Demeo Integration has thrown an exception: {ex}");
                    }
                }

                foreach (var m in AniiModMod.RegisteredAniiMods)
                {
                    try
                    {
                        ParseAniiMod(m);
                    }
                    catch (Exception ex)
                    {
                        AniiModLogger.Error($"Demeo Integration has thrown an exception: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Demeo Integration has thrown an exception: {ex}");
            }
        }

        private void OnUnregister(AniiModBase aniimod)
        {
            if (aniimod == null)
                return;

            if (!ModInformation.ContainsKey(aniimod))
                return;

            try
            {
                object info = ModInformation[aniimod];
                ModInformation.Remove(aniimod);
                ModInfoList.Remove(info);
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Demeo Integration has thrown an exception: {ex}");
            }
        }

        private void ParseAniiMod<T>(T aniimod) where T : AniiModBase
        {

            if (aniimod == null)
                return;

            if (ModInformation.ContainsKey(aniimod))
                return;

            try
            {
                object info = Activator.CreateInstance(modInfoType);

                name_field.SetValue(info, aniimod.Info.Name);
                version_field.SetValue(info, aniimod.Info.Version);
                author_field.SetValue(info, aniimod.Info.Author);
                description_field.SetValue(info, aniimod.Info.DownloadLink);
                isNetworkCompatible_field.SetValue(info, AniiModUtils.PullAttributeFromAssembly<Demeo_LobbyRequirement>(aniimod.AniiModAssembly.Assembly) == null);

                ModInformation.Add(aniimod, info);
                ModInfoList.Add(info);
            }
            catch (Exception ex)
            {
                AniiModLogger.Error($"Demeo Integration has thrown an exception: {ex}");
            }
        }
    }
}