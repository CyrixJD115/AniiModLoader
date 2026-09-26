namespace AniiModLoader
{
    // TO-DO: Replace with LemonTuple<AniiModBase[], RottenAniiMod[]>
    public sealed class ResolvedAniiMods
    {
        public readonly AniiModBase[] loadedAniiMods;
        public readonly RottenAniiMod[] rottenAniiMods;

        public ResolvedAniiMods(AniiModBase[] loadedAniiMods, RottenAniiMod[] rottenAniiMods)
        {
            this.loadedAniiMods = loadedAniiMods ?? new AniiModBase[0];
            this.rottenAniiMods = rottenAniiMods ?? new RottenAniiMod[0];
        }
    }
}
