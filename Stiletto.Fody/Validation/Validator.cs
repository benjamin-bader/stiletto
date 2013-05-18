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
﻿using Stiletto.Fody.Generators;
﻿using Stiletto.Internal;

namespace Stiletto.Fody.Validation
{
    public class Validator
    {
        private readonly IPlugin plugin;
        private readonly ICollection<ModuleGenerator> moduleGenerators;
        private readonly IDictionary<string, ModuleGenerator> modulesByTypeName; 
        private readonly IErrorReporter errorReporter;

        public Validator(
            IErrorReporter errorReporter,
            IEnumerable<InjectBindingGenerator> injectBindings,
            IEnumerable<LazyBindingGenerator> lazyBindings,
            IEnumerable<ProviderBindingGenerator> providerBindings,
            IEnumerable<ModuleGenerator> modules)
        {
            modulesByTypeName = modules.ToDictionary(m => m.ModuleType.FullName, m => m);
            moduleGenerators = modulesByTypeName.Values;
            plugin = new CompilerPlugin(injectBindings, lazyBindings, providerBindings);
            this.errorReporter = errorReporter;
        }

        public void ValidateCompleteModules()
        {
            foreach (var moduleGenerator in moduleGenerators) {
                if (!moduleGenerator.IsComplete) {
                    continue;
                }

                try {
                    var moduleBindings = ProcessCompleteModule(moduleGenerator);
                    new GraphVerifier().Verify(moduleBindings.Values);
                }
                catch (InvalidOperationException ex) {
                    errorReporter.LogError(ex.Message);
                    continue;
                }
                catch (ValidationException ex) {
                    errorReporter.LogError(ex.Message);
                    continue;
                }

                // XXX ben: Write graphviz file here.
            }
        }

        private IDictionary<string, Binding> ProcessCompleteModule(ModuleGenerator moduleGenerator)
        {
            var bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
            var allModules = new Dictionary<string, ModuleGenerator>(StringComparer.Ordinal);
            
            GatherIncludedModules(moduleGenerator, allModules, new Stack<string>());

            var resolver = new Resolver(null, plugin, errors => {
                foreach (var e in errors) {
                    errorReporter.LogError(e);
                }
            });

            foreach (var module in allModules.Values) {
                // Request entry-point bindings
                foreach (var entryPointType in module.EntryPoints) {
                    var key = CompilerKeys.GetMemberKey(entryPointType);
                    resolver.RequestBinding(CompilerKeys.ForType(entryPointType), module.ModuleType.FullName, false, true);
                    resolver.RequestBinding(key, module.ModuleType.FullName, false, true);
                }

                foreach (var providerGenerator in module.ProviderGenerators) {

                    var binding = new CompilerProvidesBinding(providerGenerator);

                    if (bindings.ContainsKey(binding.ProviderKey)) {
                        throw new ValidationException("Duplicate bindings for " + binding.ProviderKey);
                    }

                    bindings.Add(binding.ProviderKey, binding);
                }
            }

            resolver.InstallBindings(bindings);
            return resolver.ResolveAllBindings();
        }

        private void GatherIncludedModules(
            ModuleGenerator module,
            IDictionary<string, ModuleGenerator> modules,
            Stack<string> path)
        {
            var name = module.ModuleType.FullName;

            if (path.Contains(name)) {
                var sb = new StringBuilder("Circular module dependency: ");

                if (path.Count == 1) {
                    sb.AppendFormat("{0} includes itself directly.", name);
                } else {
                    var includer = name;
                    for (var i = 0; path.Count > 0; ++i) {
                        var current = includer;
                        includer = path.Pop();
                        sb.AppendLine()
                          .AppendFormat("{0}.  {1} included by {2}", i, current, includer);
                    }
                }

                throw new ValidationException(sb.ToString());
            }

            modules.Add(name, module);

            foreach (var typeReference in module.IncludedModules) {
                path.Push(name);
                GatherIncludedModules(modulesByTypeName[typeReference.FullName], modules, path);
                path.Pop();
            }
        }
    }
}
