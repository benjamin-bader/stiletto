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

        public void ValidateCompleteModules(bool suppressUnusedBindingsErrors)
        {
            foreach (var moduleGenerator in moduleGenerators) {
                if (!moduleGenerator.IsComplete) {
                    continue;
                }

                GraphVerifier graphVerifier;
                try
                {
                    var moduleBindings = ProcessCompleteModule(moduleGenerator, false);
                    if (moduleBindings == null)
                    {
                        // No bindings means that bindings could not be resolved and
                        // errors were reported.
                        continue;
                    }

                    graphVerifier = new GraphVerifier();
                    graphVerifier.DetectCircularDependencies(moduleBindings.Values, new Stack<Binding>());
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

                // TODO: This analysis is broken for entry points that are satisfied by [Provides] methods.
                if (!moduleGenerator.IsLibrary)
                {
                    try
                    {
                        var moduleBindings = ProcessCompleteModule(moduleGenerator, true);
                        graphVerifier.DetectUnusedBindings(moduleBindings.Values);
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (suppressUnusedBindingsErrors)
                        {
                            errorReporter.LogWarning(ex.Message);
                        }
                        else
                        {
                            errorReporter.LogError(ex.Message);
                        }
                    }
                    catch (ValidationException ex)
                    {
                        errorReporter.LogError(ex.Message);
                    }
                }
            }
        }

        private IDictionary<string, Binding> ProcessCompleteModule(ModuleGenerator moduleGenerator, bool ignoreCompletenessErrors)
        {
            var bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
            var overrides = new Dictionary<string, Binding>(StringComparer.Ordinal);
            var allModules = new Dictionary<string, ModuleGenerator>(StringComparer.Ordinal);
            var hasError = false;

            GatherIncludedModules(moduleGenerator, allModules, new Stack<string>());

            var resolver = new Resolver(null, plugin, errors =>
            {
                if (ignoreCompletenessErrors)
                {
                    return;
                }

                hasError = true;
                foreach (var e in errors)
                {
                    errorReporter.LogError(e);
                }
            });

            foreach (var module in allModules.Values) {
                // Request entry-point bindings
                var addTo = module.IsOverride ? overrides : bindings;

                foreach (var entryPointType in module.EntryPoints)
                {
                    var key = entryPointType.Resolve().IsInterface
                                  ? CompilerKeys.ForType(entryPointType)
                                  : CompilerKeys.GetMemberKey(entryPointType);
                    resolver.RequestBinding(key, module.ModuleType.FullName, false, true);
                }

                foreach (var providerGenerator in module.ProviderGenerators) {

                    var binding = new CompilerProvidesBinding(providerGenerator);

                    if (addTo.ContainsKey(binding.ProviderKey))
                    {
                        var message = "Duplicate bindings for {0} in {1}{2}.";
                        var addendum = module.IsOverride ? "overriding module " : string.Empty;

                        throw new ValidationException(string.Format
                            (message, binding.ProviderKey, addendum, module.ModuleType.FullName));
                    }

                    addTo.Add(binding.ProviderKey, binding);
                }
            }

            resolver.InstallBindings(bindings);
            resolver.InstallBindings(overrides);
            var allBindings = resolver.ResolveAllBindings();
            return hasError ? null : allBindings;
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
