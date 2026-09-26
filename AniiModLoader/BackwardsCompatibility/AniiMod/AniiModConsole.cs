using System;

namespace AniiModLoader
{
    [Obsolete("AniiModLoader.AniiModConsole is Only Here for Compatibility Reasons. This will be removed in a future update.", true)]
    public class AniiModConsole
    {
        [Obsolete("AniiModLoader.AniiModConsole.SetTitle is Only Here for Compatibility Reasons. Please use AniiModLoader.AniiModUtils.SetConsoleTitle instead. This will be removed in a future update.", true)]
        public static void SetTitle(string title) => AniiModUtils.SetConsoleTitle(title);
    }
}