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
﻿using System.Collections.Generic;

namespace Abra.Internal
{
    internal class RuntimeAggregationPlugin : IPlugin
    {
        private readonly List<IPlugin> plugins;

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

            this.plugins = new List<IPlugin>(plugins);
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
            for (var i = 0; i < plugins.Count; ++i) {
                try {
                    return func(plugins[i]);
                }
                catch (Exception) {
                    if (i == plugins.Count - 1) {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("Control should never reach this point.");
        }
    }
}
