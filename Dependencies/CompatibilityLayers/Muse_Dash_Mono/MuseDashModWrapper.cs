using ModHelper;

namespace AniiModLoader
{
    internal class MuseDashModWrapper : AniiModMod
    {
        internal IMod modInstance;
        public override void OnInitializeAniiMod() => modInstance.DoPatching();
    }
}