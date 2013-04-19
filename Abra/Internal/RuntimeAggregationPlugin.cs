using System;

namespace Abra.Internal
{
    internal class RuntimeAggregationPlugin : IPlugin
    {
        private readonly IPlugin[] plugins;

        internal RuntimeAggregationPlugin(params IPlugin[] plugins)
        {
            if (plugins == null)
            {
                throw new ArgumentNullException("plugins");
            }

            if (plugins.Length < 1)
            {
                throw new ArgumentException("At least one plugin must be provided.");
            }

            this.plugins = plugins;
        }

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            return GetSomethingFromPlugins(plugin =>
                plugin.GetInjectBinding(key, className, mustBeInjectable));
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            return GetSomethingFromPlugins(plugin =>
                plugin.GetLazyInjectBinding(key, requiredBy, lazyKey));
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable,
                                                 string delegateKey)
        {
            return GetSomethingFromPlugins(plugin =>
                plugin.GetIProviderInjectBinding(key, requiredBy, mustBeInjectable, delegateKey));
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            return GetSomethingFromPlugins(plugin => {
                var m = plugin.GetRuntimeModule(moduleType, moduleInstance);
                m.Module = moduleInstance ?? m.CreateModule();
                return m;
            });
        }

        private T GetSomethingFromPlugins<T>(Func<IPlugin, T> func)
        {
            for (var i = 0; i < plugins.Length; ++i) {
                try {
                    return func(plugins[i]);
                }
                catch (Exception) {
                    if (i == plugins.Length - 1) {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("Control should never reach this point.");
        }
    }
}
