using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NeoCource.Editor.Tasks
{
    internal static class TaskCheckRegistry
    {
        private static readonly Dictionary<string, ITaskCheck> keyToChecker = new(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            var iface = typeof(ITaskCheck);
            var attrType = typeof(TaskCheckAttribute);

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => SafeTypes(a))
                .Where(t => !t.IsAbstract && iface.IsAssignableFrom(t) && t.GetCustomAttribute<TaskCheckAttribute>() != null);

            foreach (var t in types)
            {
                try
                {
                    var inst = (ITaskCheck)Activator.CreateInstance(t);
                    var key = t.GetCustomAttribute<TaskCheckAttribute>().Key;
                    if (!string.IsNullOrEmpty(key)) keyToChecker[key] = inst;
                }
                catch { /* ignore bad types */ }
            }
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        public static bool TryGet(string key, out ITaskCheck check)
        {
            EnsureInitialized();
            return keyToChecker.TryGetValue(key, out check);
        }

        public static IEnumerable<string> Keys
        {
            get { EnsureInitialized(); return keyToChecker.Keys; }
        }
    }
}


