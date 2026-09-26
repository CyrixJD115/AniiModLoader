#if !NET6_0_OR_GREATER

using System;

namespace AniiModLoader.MonoInternals
{
    [Obsolete("AniiModLoader.MonoInternals.MonoLibrary is Only Here for Compatibility Reasons. Please use AniiModLoader.Utils.MonoLibrary instead. This will be removed in a future update.", true)]
    public class MonoLibrary : Utils.MonoLibrary { }
}

#endif