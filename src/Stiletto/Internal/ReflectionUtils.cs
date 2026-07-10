/*
 * Copyright © 2013 Ben Bader
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Stiletto.Internal
{
    internal static class ReflectionUtils
    {
        private static readonly HashSet<Assembly> knownAssemblies = new HashSet<Assembly>(new AssemblyComparer());
        private static readonly Dictionary<string, Type> knownTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// Looks up a type at runtime by its name. Used only by the reflection-based
        /// <c>CodegenLoader</c> fallback; the primary path is the source-generated
        /// loaders registered with <see cref="LoaderRegistry"/>.
        /// </summary>
        public static Type GetType(string fullName)
        {
            var t = Type.GetType(fullName, false);

            if (t != null)
            {
                return t;
            }

            lock (knownTypes)
            {
                if (knownTypes.TryGetValue(fullName, out t))
                {
                    return t;
                }

                ScanLoadedAssemblies();

                knownTypes.TryGetValue(fullName, out t);
                return t;
            }
        }

        private static void ScanLoadedAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; ++i)
            {
                var asm = assemblies[i];
                knownAssemblies.Add(asm);

                var types = asm.GetTypes();
                for (var j = 0; j < types.Length; ++j)
                {
                    var t = types[j];

                    if (knownTypes.ContainsKey(t.FullName))
                    {
                        continue;
                    }

                    knownTypes[t.FullName] = t;
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
    }
}
