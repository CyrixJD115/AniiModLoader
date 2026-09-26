using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AniiModLoader.Logging;

#if NET6_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace AniiModLoader.Modules
{
    /// <summary>
    /// A base for external AniiModLoader modules.
    /// </summary>
    public abstract class AniiModModule
    {
        private Type moduleType;

        public string Name { get; private set; }
        public Assembly Assembly { get; private set; }
        public Info ModuleInfo { get; private set; }
        protected AniiModLogger.Instance LoggerInstance { get; private set; }

        protected AniiModModule() { }

        public virtual void OnInitialize() { }

        internal static AniiModModule Load(Info moduleInfo)
        {
            if (!File.Exists(moduleInfo.fullPath))
            {
                AniiModDebug.Msg($"AniiModModule '{moduleInfo.fullPath}' doesn't exist, ignoring.");
                return null;
            }

            if (moduleInfo.shouldBeRemoved != null && moduleInfo.shouldBeRemoved())
            {
                AniiModDebug.Msg($"Removing AniiModModule '{moduleInfo.fullPath}'...");
                try
                {
                    File.Delete(moduleInfo.fullPath);
                }
                catch (Exception ex)
                {
                    AniiModLogger.Warning($"Failed to remove AniiModModule '{moduleInfo.fullPath}':\n{ex}");
                }
                return null;
            }

            if (moduleInfo.shouldBeIgnored != null && moduleInfo.shouldBeIgnored())
            {
                AniiModDebug.Msg($"Ignoring AniiModModule '{moduleInfo.fullPath}'...");
                return null;
            }

            Assembly asm;
            try
            {
#if NET6_0_OR_GREATER
                asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(moduleInfo.fullPath);
#else
                asm = Assembly.LoadFrom(moduleInfo.fullPath);
#endif
            }
            catch (Exception ex)
            {
                AniiModLogger.Warning($"Failed to load Assembly of AniiModModule '{moduleInfo.fullPath}':\n{ex}");
                return null;
            }

            var name = asm.GetName().Name;

            var type = asm.GetTypes().FirstOrDefault(x => typeof(AniiModModule).IsAssignableFrom(x));
            if (type == null)
            {
                AniiModLogger.Warning($"Failed to load AniiModModule '{moduleInfo.fullPath}': No type deriving from AniiModModule found.");
                return null;
            }

            AniiModModule obj;
            try
            {
                obj = (AniiModModule)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null);
            }
            catch (Exception ex)
            {
                AniiModLogger.Warning($"Failed to initialize AniiModModule '{moduleInfo.fullPath}':\n{ex}");
                return null;
            }

            obj.moduleType = type;
            obj.Name = name;
            obj.Assembly = asm;
            obj.ModuleInfo = moduleInfo;
            obj.LoggerInstance = new AniiModLogger.Instance(name, ColorARGB.Magenta); // Magenta cool :)

            try
            {
                obj.OnInitialize();
            }
            catch (Exception ex)
            {
                obj.LoggerInstance.Error($"Local initialization failed:\n{ex}");
                return null;
            }

            return obj;
        }

        public object SendMessage(string name, params object[] arguments)
        {
            var msg = moduleType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (msg == null)
                return null;

            return msg.Invoke(msg.IsStatic ? null : this, arguments);
        }

        public class Info
        {
            public readonly string fullPath;
            internal readonly Func<bool> shouldBeRemoved;
            internal readonly Func<bool> shouldBeIgnored;
            internal AniiModModule moduleGC;

            internal Info(string path, Func<bool> shouldBeIgnored = null, Func<bool> shouldBeRemoved = null)
            {
                fullPath = Path.GetFullPath(path);
                this.shouldBeRemoved = shouldBeRemoved;
                this.shouldBeIgnored = shouldBeIgnored;
            }
        }
    }
}
