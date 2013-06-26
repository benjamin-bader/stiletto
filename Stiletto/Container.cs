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
using System.Linq;
using System.Text;

using Stiletto.Internal;
using Stiletto.Internal.Plugins.Codegen;
using Stiletto.Internal.Plugins.Reflection;

namespace Stiletto
{
    public abstract class Container
    {
        public abstract Container Add(params object[] modules);
        public abstract T Get<T>();
        public abstract T Inject<T>(T instance);
        public abstract object Inject(object instance, Type type);
        public abstract void Validate();

        /// <summary>
        /// Creates a container with the given modules, of which at least one
        /// must be given.
        /// </summary>
        /// <param name="modules">
        /// One or more modules, which may be either a <see cref="Type"/> that
        /// is decorated with a <see cref="ModuleAttribute"/>, or an instance
        /// of such a type.
        /// </param>
        /// <returns>
        /// Returns a <see cref="Container"/> which satisfies the requirements
        /// of the given modules, if possible.
        /// </returns>
        public static Container Create(params object[] modules)
        {
            var plugins = ReflectionUtils.GetCompiledPlugins();
            plugins.Add(new CodegenPlugin());
            plugins.Add(new ReflectionPlugin());

            var plugin = new RuntimeAggregationPlugin(plugins.ToArray());
            return StilettoContainer.MakeContainer(null, plugin, modules);
        }

        public static Container CreateWithPlugins(object[] modules, IPlugin[] plugins)
        {
            var allPlugins = new IPlugin[plugins.Length + 2];
            Array.Copy(plugins, allPlugins, plugins.Length);
            allPlugins[plugins.Length] = new CodegenPlugin();
            allPlugins[plugins.Length + 1] = new ReflectionPlugin();
            return StilettoContainer.MakeContainer(null, new RuntimeAggregationPlugin(allPlugins), modules);
        }

        private class StilettoContainer : Container
        {
            private readonly StilettoContainer baseContainer;
            private readonly Resolver resolver;
            private readonly IDictionary<string, Type> injectTypes;
            private readonly IPlugin plugin;

            private StilettoContainer(
                StilettoContainer baseContainer,
                Resolver resolver,
                IPlugin plugin,
                IDictionary<string, Type> injectTypes)
            {
                this.baseContainer = baseContainer;
                this.resolver = resolver;
                this.plugin = plugin;
                this.injectTypes = injectTypes;
            }

            internal static StilettoContainer MakeContainer(
                StilettoContainer baseContainer,
                IPlugin plugin,
                params object[] modules)
            {
                var injectTypes = new Dictionary<string, Type>(Key.Comparer);
                var bindings = new Dictionary<string, Binding>(Key.Comparer);
                var overrides = new Dictionary<string, Binding>(Key.Comparer);

                foreach (var runtimeModule in GetAllRuntimeModules(plugin, modules).Values)
                {
                    var addTo = runtimeModule.IsOverride ? overrides : bindings;

                    foreach (var key in runtimeModule.Injects)
                    {
                        injectTypes.Add(key, runtimeModule.Module.GetType());
                    }

                    runtimeModule.GetBindings(addTo);
                }

                var resolver = new Resolver(
                    baseContainer != null ? baseContainer.resolver : null,
                    plugin,
                    HandleErrors);

                resolver.InstallBindings(bindings);
                resolver.InstallBindings(overrides);

                return new StilettoContainer(baseContainer, resolver, plugin, injectTypes);
            }

            public override Container Add(params object[] modules)
            {
                ResolveAllBindings();
                return MakeContainer(this, plugin, modules);
            }

            public override T Get<T>()
            {
                var key = Key.Get<T>();
                var injectKey = Key.GetMemberKey<T>();
                var binding = GetInjectBinding(injectKey, key);
                return (T) binding.Get();
            }

            public override T Inject<T>(T instance)
            {
                var key = Key.GetMemberKey<T>();
                var binding = GetInjectBinding(key, key);
                binding.InjectProperties(instance);
                return instance;
            }

            public override object Inject(object instance, Type type)
            {
                var key = Key.GetMemberKey(type);
                var binding = GetInjectBinding(key, key);
                binding.InjectProperties(instance);
                return instance;
            }

            public override void Validate()
            {
                var allBindings = ResolveAllBindings();
                new GraphVerifier().Verify(allBindings.Values);
            }

            private IDictionary<string, Binding> ResolveAllBindings()
            {
                lock (resolver)
                {
                    ResolveInjectTypes();
                    return resolver.ResolveAllBindings();
                }
            }

            private void ResolveInjectTypes()
            {
                foreach (var kvp in injectTypes)
                {
                    resolver.RequestBinding(kvp.Key, kvp.Value, false);
                }
            }

            private Binding GetInjectBinding(string membersKey, string key)
            {
                Type moduleType = null;
                for (var container = this; container != null; container = container.baseContainer)
                {
                    if (container.injectTypes.TryGetValue(membersKey, out moduleType) && moduleType != null)
                        break;
                }

                if (moduleType == null)
                {
                    throw new ArgumentException("No Injects entry for " + membersKey + ".  You must add this type to one of your modules' Injects list.");
                }

                lock (resolver)
                {
                    var binding = resolver.RequestBinding(key, moduleType, false);
                    if (binding == null || !binding.IsResolved)
                    {
                        resolver.ResolveEnqueuedBindings();
                        binding = resolver.RequestBinding(key, moduleType, false);
                    }

                    return binding;
                }
            }

            private static void HandleErrors(IEnumerable<string> errors)
            {
                var sb = new StringBuilder("The following errors were detected while constructing your object graph:")
                        .AppendLine();
                var message = errors.Aggregate(sb, (s, error) => s.AppendLine(error)).ToString();

                throw new InvalidOperationException(message);
            }

            private static IDictionary<Type, RuntimeModule> GetAllRuntimeModules(
                IPlugin plugin,
                object[] seedModules)
            {
                var runtimeModules = new RuntimeModule[seedModules.Length];
                for (var i = 0; i < runtimeModules.Length; ++i)
                {
                    var m = seedModules[i];
                    if (m is Type)
                    {
                        runtimeModules[i] = plugin.GetRuntimeModule((Type)m, null);
                    }
                    else
                    {
                        runtimeModules[i] = plugin.GetRuntimeModule(m.GetType(), m);
                    }
                }

                var result = new Dictionary<Type, RuntimeModule>();
                for (var i = 0; i < seedModules.Length; ++i)
                {
                    result.Add(runtimeModules[i].Module.GetType(), runtimeModules[i]);
                }

                // Fan out and instantiate all modules included by the seed modules
                var toInclude = new Queue<Type>();
                for (var i = 0; i < runtimeModules.Length; ++i)
                {
                    var includes = runtimeModules[i].Includes;
                    for (var j = 0; j < includes.Length; ++j)
                    {
                        toInclude.Enqueue(includes[j]);
                    }
                }

                while (toInclude.Count > 0)
                {
                    var t = toInclude.Dequeue();
                    if (result.ContainsKey(t))
                    {
                        continue;
                    }

                    var runtimeModule = plugin.GetRuntimeModule(t, null);
                    result.Add(t, runtimeModule);

                    for (var i = 0; i < runtimeModule.Includes.Length; ++i)
                    {
                        toInclude.Enqueue(runtimeModule.Includes[i]);
                    }
                }

                return result;
            }
        }
    }
}
