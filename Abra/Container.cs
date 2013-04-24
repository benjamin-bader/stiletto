using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Abra.Internal;
using Abra.Internal.Plugins.Codegen;
using Abra.Internal.Plugins.Reflection;

namespace Abra
{
    public abstract class Container
    {
        public abstract Container Add(params object[] modules);
        public abstract T Get<T>();
        public abstract T Inject<T>(T instance);
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
            var plugin = new RuntimeAggregationPlugin(
                new CodegenPlugin(), new ReflectionPlugin());
            return AbraContainer.MakeContainer(null, plugin, modules);
        }

        public static Container CreateWithPlugin(IPlugin plugin, params object[] modules)
        {
            plugin = new RuntimeAggregationPlugin(plugin, new CodegenPlugin());
            return AbraContainer.MakeContainer(null, plugin, modules);
        }

        private class AbraContainer : Container
        {
            private readonly AbraContainer baseContainer;
            private readonly Resolver resolver;
            private readonly IDictionary<string, Type> entryPoints;
            private readonly IPlugin plugin;

            private AbraContainer(
                AbraContainer baseContainer,
                Resolver resolver,
                IPlugin plugin,
                IDictionary<string, Type> entryPoints)
            {
                this.baseContainer = baseContainer;
                this.resolver = resolver;
                this.plugin = plugin;
                this.entryPoints = entryPoints;
            }

            internal static AbraContainer MakeContainer(
                AbraContainer baseContainer,
                IPlugin plugin,
                params object[] modules)
            {
                var entryPoints = new Dictionary<string, Type>(Key.Comparer);
                var bindings = new Dictionary<string, Binding>(Key.Comparer);
                foreach (var runtimeModule in GetAllRuntimeModules(plugin, modules).Values)
                {
                    foreach (var key in runtimeModule.EntryPoints)
                    {
                        entryPoints.Add(key, runtimeModule.Module.GetType());
                    }

                    runtimeModule.GetBindings(bindings);
                }

                var resolver = new Resolver(
                    baseContainer != null ? baseContainer.resolver : null,
                    plugin,
                    HandleErrors);
                resolver.InstallBindings(bindings);

                return new AbraContainer(baseContainer, resolver, plugin, entryPoints);
            }

            public override Container Add(params object[] modules)
            {
                ResolveAllBindings();
                return MakeContainer(this, plugin, modules);
            }

            public override T Get<T>()
            {
                var key = Key.Get<T>();
                var entryPointKey = Key.GetMemberKey<T>();
                var binding = GetEntryPointBinding(entryPointKey, key);
                return (T) binding.Get();
            }

            public override T Inject<T>(T instance)
            {
                var key = Key.GetMemberKey<T>();
                var binding = GetEntryPointBinding(key, key);
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
                    ResolveEntryPoints();
                    return resolver.ResolveAllBindings();
                }
            }

            private void ResolveEntryPoints()
            {
                foreach (var kvp in entryPoints)
                {
                    resolver.RequestBinding(kvp.Key, kvp.Value, false);
                }
            }

            private Binding GetEntryPointBinding(string entryPointKey, string key)
            {
                Type moduleType = null;
                for (var container = this; container != null; container = container.baseContainer)
                {
                    if (container.entryPoints.TryGetValue(entryPointKey, out moduleType) && moduleType != null)
                        break;
                }

                if (moduleType == null)
                {
                    throw new ArgumentException("No entry point for " + entryPointKey + ".  You must add an entry point to one of your modules.");
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
