using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Abra.Internal
{
    internal class ReflectionUtils
    {
        private static HashSet<Assembly> knownAssemblies = new HashSet<Assembly>(new AssemblyComparer());

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

            lock (knownAssemblies) {
                t = GetTypeFromKnownAssemblies(fullName);
            }

            if (t != null) {
                return t;
            }

            lock (knownAssemblies) {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    knownAssemblies.Add(assembly);
                }

                return GetTypeFromKnownAssemblies(fullName);
            }

        }

        private static Type GetTypeFromKnownAssemblies(string fullName)
        {
            Type t = null;
            foreach (var assembly in knownAssemblies) {
                t = assembly.GetType(fullName, false);

                if (t != null) {
                    break;
                }
            }
            return t;
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
    }
}
