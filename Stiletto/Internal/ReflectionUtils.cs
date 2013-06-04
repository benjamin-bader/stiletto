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

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
﻿using Stiletto.Internal.Plugins.Codegen;

namespace Stiletto.Internal
{
    internal class ReflectionUtils
    {
        private static HashSet<Assembly> knownAssemblies = new HashSet<Assembly>(new AssemblyComparer());
        private static Dictionary<string, Type> knownTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static IList<IPlugin> plugins;

        static ReflectionUtils()
        {
#if SILVERLIGHT
            plugins = new List<IPlugin>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (!knownAssemblies.Add(assembly)) {
                    continue;
                }

                var plugin = assembly.GetType(CodegenPlugin.CompiledPluginFullName, false);

                if (plugin == null) {
                    continue;
                }

                plugins.Insert(0, (IPlugin) Activator.CreateInstance(plugin));
            }
#else
            AppDomain.CurrentDomain.AssemblyLoad += (o, e) => {
                lock (knownAssemblies) {
                    if (!knownAssemblies.Add(e.LoadedAssembly)) {
                        return;
                    }

                    var plugin = e.LoadedAssembly.GetType(CodegenPlugin.CompiledPluginFullName, false);

                    if (plugin == null) {
                        return;
                    }

                    plugins.Insert(0, (IPlugin) Activator.CreateInstance(plugin));
                }
            };
#endif
        }

        public static IList<IPlugin> GetCompiledPlugins()
        {
            if (plugins != null) {
                return plugins;
            }

            lock (knownAssemblies) {
                plugins = new List<IPlugin>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    knownAssemblies.Add(assembly);

                    var t = assembly.GetType(CodegenPlugin.CompiledPluginFullName);

                    if (t == null) {
                        continue;
                    }

                    var p = Activator.CreateInstance(t) as IPlugin;

                    if (p == null) {
                        continue;
                    }

                    plugins.Add(p);
                }
            }

            return plugins;
        }

        public bool HasAssemblyBeenExamined<T>()
        {
            return knownAssemblies.Contains(typeof (T).Assembly);
        }

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

        private static void ScanLoadedAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; ++i) {
                var asm = assemblies[i];
                knownAssemblies.Add(asm);

                var types = asm.GetTypes();
                for (var j = 0; j < types.Length; ++j) {
                    var t = types[j];

                    if (knownTypes.ContainsKey(t.FullName)) {
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
