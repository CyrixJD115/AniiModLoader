namespace AniiModLoader
{
    public static class AniiMod<T> where T : AniiModBase
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var aniimod = AniiModAssembly.FindAniiModInstance<T>();
                if (aniimod == null)
                    return null;

                _instance = aniimod;
                return aniimod;
            }
        }

        public static AniiModLogger.Instance Logger => Instance?.LoggerInstance;
    }
}
