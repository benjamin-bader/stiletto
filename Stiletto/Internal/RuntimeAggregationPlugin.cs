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

namespace Stiletto.Internal
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
            for (var i = 0; i < plugins.Count; ++i) {
                try {
                    var binding = plugins[i].GetInjectBinding(key, className, mustBeInjectable);
                    if (binding == null) {
                        if (i == plugins.Count - 1) {
                            throw new InvalidOperationException("Could not load inject binding: " + key);
                        }

                        continue;
                    }
                    return binding;
                }
                catch (Exception) {
                    if (i == plugins.Count - 1) {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("Control should never reach this point.");
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            for (var i = 0; i < plugins.Count; ++i) {
                try {
                    var binding = plugins[i].GetLazyInjectBinding(key, requiredBy, lazyKey);
                    if (binding == null) {
                        if (i == plugins.Count - 1) {
                            throw new InvalidOperationException("Could not load lazy binding " + key);
                        }

                        continue;
                    }
                    return binding;
                }
                catch (Exception) {
                    if (i == plugins.Count - 1) {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("Control should never reach this point.");
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable,
                                                 string delegateKey)
        {
            for (var i = 0; i < plugins.Count; ++i) {
                try {
                    var binding = plugins[i].GetIProviderInjectBinding(key, requiredBy, mustBeInjectable, delegateKey);
                    if (binding == null) {
                        if (i == plugins.Count - 1) {
                            throw new InvalidOperationException("Could not load provider binding: " + key);
                        }

                        continue;
                    }
                    return binding;
                }
                catch (Exception) {
                    if (i == plugins.Count - 1) {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("Control should never reach this point.");
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            for (var i = 0; i < plugins.Count; ++i) {
                try {
                    var m = plugins[i].GetRuntimeModule(moduleType, moduleInstance);

                    if (m == null) {
                        if (i == plugins.Count - 1) {
                            throw new InvalidOperationException("Could not load runtime module: " + moduleType);
                        }

                        continue;
                    }

                    m.Module = moduleInstance ?? m.CreateModule();
                    return m;
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
