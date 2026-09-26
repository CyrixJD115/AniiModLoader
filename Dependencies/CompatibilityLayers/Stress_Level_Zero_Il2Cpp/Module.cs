using AniiModLoader.Modules;
using System.Collections.Generic;

[assembly: AniiModLoader.PatchShield]

namespace AniiModLoader.CompatibilityLayers
{
    internal class SLZ_Module : AniiModModule
    {
        private static bool HasGotLoadingSceneIndex = false;
        private static int LoadingSceneIndex = -9;

        private static Dictionary<string, int> CompatibleGames = new Dictionary<string, int>
        {
            ["BONELAB"] = 0,
            ["BONEWORKS"] = 0
        };

        public override void OnInitialize()
        {
            if (!CompatibleGames.ContainsKey(InternalUtils.UnityInformationHandler.GameName))
                return;

            AniiModEvents.OnSceneWasLoaded.Subscribe(OnSceneLoad, int.MinValue);
            AniiModEvents.OnSceneWasInitialized.Subscribe(OnSceneInit, int.MinValue);
        }

        private static void OnSceneLoad(int buildIndex, string name)
        {
            if (HasGotLoadingSceneIndex)
            {
                if (buildIndex == LoadingSceneIndex)
                    PreSceneEvent();
                return;
            }

            if (buildIndex == 0)
                return;

            HasGotLoadingSceneIndex = true;
            LoadingSceneIndex = buildIndex;
            PreSceneEvent();
        }

        private static void OnSceneInit(int buildIndex, string name)
        {
            if (!HasGotLoadingSceneIndex
                || (buildIndex != LoadingSceneIndex))
                return;

            PostSceneEvent();
            AniiModBase.SendMessageAll("OnLoadingScreen");
            AniiModBase.SendMessageAll($"{InternalUtils.UnityInformationHandler.GameName}_OnLoadingScreen");
        }

        private static AniiModEvent<int, string>.AniiModEventSubscriber[] SceneInitBackup;
        private static void PreSceneEvent()
        {
            SceneInitBackup = AniiModEvents.OnSceneWasInitialized.GetSubscribers();
            AniiModEvents.OnSceneWasInitialized.UnsubscribeAll();
            AniiModEvents.OnSceneWasInitialized.Subscribe(OnSceneInit, int.MinValue);
        }
        private static void PostSceneEvent()
        {
            AniiModEvents.OnSceneWasInitialized.UnsubscribeAll();
            foreach (var sub in SceneInitBackup)
                AniiModEvents.OnSceneWasInitialized.Subscribe(sub.del, sub.priority, sub.unsubscribeOnFirstInvocation);
            SceneInitBackup = null;
        }
    }
}