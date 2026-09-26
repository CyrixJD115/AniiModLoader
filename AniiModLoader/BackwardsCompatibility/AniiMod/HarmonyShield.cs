using System;

namespace Harmony
{
    [Obsolete("Harmony.HarmonyShield is Only Here for Compatibility Reasons. Please use AniiModLoader.PatchShield instead. This will be removed in a future update.", true)]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
    public class HarmonyShield : AniiModLoader.PatchShield { }
}
