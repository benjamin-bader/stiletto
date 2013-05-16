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
using System.Collections.Generic;
using System.Reflection;
﻿using Abra.Internal.Plugins.Codegen;

namespace Abra.Internal
{
    internal class ReflectionUtils
    {
        private static HashSet<Assembly> knownAssemblies = new HashSet<Assembly>(new AssemblyComparer());
        private static IList<IPlugin> plugins;

        static ReflectionUtils()
        {
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

        public static IPlugin FindCompiledPlugin()
        {
            IPlugin plugin;

            lock (knownAssemblies) {
                plugin = GetPluginFromKnownAssemblies();
            }

            if (plugin != null) {
                return plugin;
            }

            lock (knownAssemblies) {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    knownAssemblies.Add(asm);
                }

                return GetPluginFromKnownAssemblies();
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

        private static IPlugin GetPluginFromKnownAssemblies()
        {
            foreach (var asm in knownAssemblies) {
                if (asm.FullName.StartsWith("Abra")) {
                    continue;
                }

                var types = asm.GetTypes();
                for (var i = 0; i < types.Length; ++i) {
                    var t = types[i];
                    var iface = t.GetInterface("Abra.Internal.IPlugin", false);
                    if (iface == null) {
                        continue;
                    }
                    if (t.GetConstructor(Type.EmptyTypes) == null) {
                        continue;
                    }
                    return (IPlugin) Activator.CreateInstance(t);
                }
            }

            return null;
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
