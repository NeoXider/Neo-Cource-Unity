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

        public static IEnumerable<string> Keys
        {
            get
            {
                EnsureInitialized();
                return keyToChecker.Keys;
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Type iface = typeof(ITaskCheck);
            Type attrType = typeof(TaskCheckAttribute);

            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => SafeTypes(a))
                .Where(t => !t.IsAbstract && iface.IsAssignableFrom(t) &&
                            t.GetCustomAttribute<TaskCheckAttribute>() != null);

            foreach (Type t in types)
            {
                try
                {
                    ITaskCheck inst = (ITaskCheck)Activator.CreateInstance(t);
                    string key = t.GetCustomAttribute<TaskCheckAttribute>().Key;
                    if (!string.IsNullOrEmpty(key))
                    {
                        keyToChecker[key] = inst;
                    }
                }
                catch
                {
                    /* ignore bad types */
                }
            }
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        public static bool TryGet(string key, out ITaskCheck check)
        {
            EnsureInitialized();
            return keyToChecker.TryGetValue(key, out check);
        }
    }
}