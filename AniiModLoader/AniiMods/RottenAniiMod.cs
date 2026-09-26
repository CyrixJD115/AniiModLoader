using System;
using System.Reflection;

namespace AniiModLoader
{
    /// <summary>
    /// An info class for broken AniiMods.
    /// </summary>
    public sealed class RottenAniiMod
    {
        public readonly Assembly assembly;
        public readonly Type type;
        public readonly string errorMessage;
        public readonly string exception;

        public RottenAniiMod(AniiModAssembly assembly, string errorMessage)
            : this(assembly.Assembly, errorMessage, string.Empty) { }
        public RottenAniiMod(AniiModAssembly assembly, string errorMessage, string exception)
            : this(assembly.Assembly, errorMessage, exception) { }
        public RottenAniiMod(AniiModAssembly assembly, string errorMessage, Exception exception)
            : this(assembly.Assembly, errorMessage, ((exception != null) ? exception.ToString() : null)) { }

        public RottenAniiMod(Type type, string errorMessage)
            : this(type.Assembly, errorMessage, string.Empty)
            => this.type = type;
        public RottenAniiMod(Type type, string errorMessage, string exception)
            : this(type.Assembly, errorMessage, exception)
            => this.type = type;
        public RottenAniiMod(Type type, string errorMessage, Exception exception)
            : this(type.Assembly, errorMessage, ((exception != null) ? exception.ToString() : null))
            => this.type = type;

        public RottenAniiMod(Assembly assembly, string errorMessage)
            : this(assembly, errorMessage, string.Empty) { }
        public RottenAniiMod(Assembly assembly, string errorMessage, Exception exception)
            : this(assembly, errorMessage, ((exception != null) ? exception.ToString() : null)) { }
        public RottenAniiMod(Assembly assembly, string errorMessage, string exception)
        {
            this.assembly = assembly;
            this.errorMessage = errorMessage;
            this.exception = exception;
        }
    }
}
