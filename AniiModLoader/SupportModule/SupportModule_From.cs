namespace AniiModLoader
{
    internal class SupportModule_From : ISupportModule_From
    {
        public void OnApplicationLateStart()
            => AniiModEvents.OnApplicationLateStart.Invoke();

        public void OnSceneWasLoaded(int buildIndex, string sceneName)
            => AniiModEvents.OnSceneWasLoaded.Invoke(buildIndex, sceneName);

        public void OnSceneWasInitialized(int buildIndex, string sceneName)
            => AniiModEvents.OnSceneWasInitialized.Invoke(buildIndex, sceneName);

        public void OnSceneWasUnloaded(int buildIndex, string sceneName)
            => AniiModEvents.OnSceneWasUnloaded.Invoke(buildIndex, sceneName);

        public void Update()
            => AniiModEvents.OnUpdate.Invoke();

        public void FixedUpdate()
            => AniiModEvents.OnFixedUpdate.Invoke();

        public void LateUpdate()
            => AniiModEvents.OnLateUpdate.Invoke();

        public void OnGUI()
            => AniiModEvents.OnGUI.Invoke();

        public void Quit()
            => AniiModEvents.OnApplicationQuit.Invoke();

        public void DefiniteQuit()
        {
            AniiModEvents.OnApplicationDefiniteQuit.Invoke();
            Core.Quit();
        }

        public void SetInteropSupportInterface(InteropSupport.Interface interop)
        {
            if (InteropSupport.SMInterface == null)
                InteropSupport.SMInterface = interop;
        }
    }
}