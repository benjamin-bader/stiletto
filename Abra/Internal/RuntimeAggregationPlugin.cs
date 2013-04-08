using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            for (var i = 0; i < plugins.Length; ++i)
            {
                try
                {
                    return plugins[i].GetInjectBinding(key, className, mustBeInjectable);
                }
                catch (Exception)
                {
                    if (i == plugins.Length - 1)
                        throw;
                }
            }
            throw new InvalidOperationException("Control should never flow to this point.");
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            for (var i = 0; i < plugins.Length; ++i)
            {
                try
                {
                    var m = plugins[i].GetRuntimeModule(moduleType, moduleInstance);
                    m.Module = moduleInstance ?? m.CreateModule();
                    return m;
                }
                catch (Exception)
                {
                    if (i == plugins.Length - 1)
                        throw;
                }
            }

            throw new InvalidOperationException("Control should never flow to this point.");
        }
    }
}
