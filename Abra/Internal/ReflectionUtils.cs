using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abra.Internal
{
    internal class ReflectionUtils
    {
        private readonly static HashSet<Assembly> knownAssemblies = new HashSet<Assembly>(new AssemblyComparer());
        private readonly static Dictionary<string, Type> knownTypes = new Dictionary<string, Type>();
        private readonly static HashSet<Type> pluginTypes = new HashSet<Type>(new TypeComparer());

        /// <summary>
        /// Looks up a type at runtime by its name.
        /// </summary>
        /// <remarks>
        /// This is currently one of the biggest drains on performance.
        /// If we could coax NRefactory to give us assembly-qualified names,
        /// this could be much more efficient.
        /// </remarks>
        /// <param name="fullName"></param>
        /// <returns></returns>
        public static Type GetType(string fullName)
        {
            var t = Type.GetType(fullName, false);

            if (t != null) {
                return t;
            }

            lock (knownTypes) {
                if (knownTypes.TryGetValue(fullName, out t)) {
                    return t;
                }

                ScanLoadedAssemblies();

                knownTypes.TryGetValue(fullName, out t);
                return t;
            }
        }

        public static IList<IPlugin> FindCompiledPlugins()
        {
            lock (knownAssemblies) {
                if (pluginTypes.Count != 0) {
                    return pluginTypes
                        .Select(Activator.CreateInstance)
                        .Cast<IPlugin>()
                        .ToList();
                }

                ScanLoadedAssemblies();

                return pluginTypes
                    .Select(Activator.CreateInstance)
                    .Cast<IPlugin>()
                    .ToList();
            }
        }

        private static void ScanLoadedAssemblies()
        {
            var pluginType = typeof (IPlugin);

            var containingAssembly = Assembly.GetExecutingAssembly();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; ++i) {
                var asm = assemblies[i];
                if (!knownAssemblies.Add(asm)) {
                    continue;
                }

                var isInternalAsm = asm.FullName.Equals(containingAssembly.FullName, StringComparison.Ordinal);

                var types = asm.GetTypes();
                for (var j = 0; j < types.Length; ++j) {
                    var t = types[j];

                    if (knownTypes.ContainsKey(t.FullName)) {
                        continue;
                    }

                    knownTypes[t.FullName] = t;

                    if (isInternalAsm) {
                        continue;
                    }

                    var iface = t.GetInterface(pluginType.FullName);
                    
                    if (iface == null) {
                        continue;
                    }

                    pluginTypes.Add(t);
                }
            }
        }

        private class AssemblyComparer : IEqualityComparer<Assembly>
        {
            public bool Equals(Assembly x, Assembly y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                return x.FullName.Equals(y.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(Assembly obj)
            {
                return ReferenceEquals(obj, null) ? 0 : obj.FullName.GetHashCode();
            }
        }

        private class TypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                return x.FullName.Equals(y.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(Type obj)
            {
                return ReferenceEquals(obj, null) ? 0 : obj.FullName.GetHashCode();
            }
        }
    }
}
