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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil;
using Stiletto.Fody.Generators;
﻿using Stiletto.Internal;
using Stiletto.Internal.Loaders.Codegen;

namespace Stiletto.Fody.Validation
{
    public class Validator
    {
        private readonly ICollection<ModuleGenerator> moduleGenerators;
        private readonly IDictionary<string, ModuleGenerator> modulesByTypeName;
        private readonly IErrorReporter errorReporter;
        private readonly IEnumerable<InjectBindingGenerator> injectBindings;
        private readonly IEnumerable<LazyBindingGenerator> lazyBindings; 
        private readonly IEnumerable<ProviderBindingGenerator> providerBindings; 

        private ILoader loader;

        public Validator(
            IErrorReporter errorReporter,
            IEnumerable<InjectBindingGenerator> injectBindings,
            IEnumerable<LazyBindingGenerator> lazyBindings,
            IEnumerable<ProviderBindingGenerator> providerBindings,
            IEnumerable<ModuleGenerator> modules)
        {
            modulesByTypeName = modules.ToDictionary(m => m.ModuleType.FullName, m => m);
            moduleGenerators = modulesByTypeName.Values;
            this.injectBindings = injectBindings;
            this.lazyBindings = lazyBindings;
            this.providerBindings = providerBindings;
            //loader = new CompilerLoader(injectBindings, lazyBindings, providerBindings);
            this.errorReporter = errorReporter;
        }

        public void ValidateCompleteModules(bool suppressUnusedBindingsErrors, bool suppressGraphviz, string outputDirectory)
        {
            string graphvizDirectory = null;
            if (!suppressGraphviz)
            {
                graphvizDirectory = PrepareGraphvizDirectory(outputDirectory);
            }

            var invalidModules = new HashSet<ModuleGenerator>();
            foreach (var moduleGenerator in moduleGenerators)
            {
                if (!moduleGenerator.IsComplete)
                {
                    continue;
                }

                try
                {
                    var modules = new Dictionary<string, ModuleGenerator>();
                    GatherIncludedModules(moduleGenerator, modules, new Stack<string>());

                    var injectTypesByModule = new Dictionary<TypeReference, IList<ModuleGenerator>>(new TypeReferenceComparer());
                    foreach (var module in modules.Values)
                    {
                        foreach (var injectType in module.Injects)
                        {
                            IList<ModuleGenerator> providingModules;
                            if (!injectTypesByModule.TryGetValue(injectType, out providingModules))
                            {
                                injectTypesByModule[injectType] = providingModules = new List<ModuleGenerator>();
                            }

                            providingModules.Add(module);
                        }
                    }

                    foreach (var kvp in injectTypesByModule)
                    {
                        var type = kvp.Key;
                        var providingModules = kvp.Value;

                        if (providingModules.Count == 1)
                        {
                            continue;
                        }

                        var sb = new StringBuilder();
                        sb.Append("The type ")
                            .Append(type.FullName)
                            .AppendLine(" is provided multiple types in one complete module network:");

                        for (var i = 0; i < providingModules.Count; ++i)
                        {
                            sb.Append("  ")
                                .Append(i + 1)
                                .Append(": ")
                                .AppendLine(providingModules[i].ModuleType.FullName);
                        }

                        errorReporter.LogError(sb.ToString());
                        invalidModules.Add(moduleGenerator);
                    }
                }
                catch (ValidationException ex)
                {
                    errorReporter.LogError(ex.Message);
                    invalidModules.Add(moduleGenerator);
                }
            }

            if (invalidModules.Count > 0)
            {
                return;
            }

            loader = new CompilerLoader(injectBindings, lazyBindings, providerBindings);

            foreach (var moduleGenerator in moduleGenerators)
            {
                if (!moduleGenerator.IsComplete)
                {
                    continue;
                }

                GraphVerifier graphVerifier;
                IDictionary<string, Binding> moduleBindings;

                try
                {
                    moduleBindings = ProcessCompleteModule(moduleGenerator, false);
                    if (moduleBindings == null)
                    {
                        // No bindings means that bindings could not be resolved and
                        // errors were reported.
                        continue;
                    }

                    graphVerifier = new GraphVerifier();
                    graphVerifier.DetectCircularDependencies(moduleBindings.Values, new Stack<Binding>());
                }
                catch (InvalidOperationException ex)
                {
                    errorReporter.LogError(ex.Message);
                    continue;
                }
                catch (ValidationException ex)
                {
                    errorReporter.LogError(ex.Message);
                    continue;
                }

                try
                {
                    if (!suppressGraphviz)
                    {
                        WriteModuleGraph(moduleGenerator, moduleBindings, graphvizDirectory);
                    }
                }
                catch (IOException ex)
                {
                    errorReporter.LogWarning("Graph visualization failed: " + ex.Message);
                }
                catch (Exception ex)
                {
                    errorReporter.LogWarning("Graph visualization failed, please report this as a bug: " + Environment.NewLine + ex);
                }

                // TODO: This analysis is broken for entry points that are satisfied by [Provides] methods.
                if (!moduleGenerator.IsLibrary)
                {
                    try
                    {
                        moduleBindings = ProcessCompleteModule(moduleGenerator, true);
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

        private IDictionary<string, Binding> ProcessCompleteModule(
            ModuleGenerator moduleGenerator,
            bool ignoreCompletenessErrors)
        {
            var bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
            var overrides = new Dictionary<string, Binding>(StringComparer.Ordinal);
            var allModules = new Dictionary<string, ModuleGenerator>(StringComparer.Ordinal);
            var hasError = false;

            GatherIncludedModules(moduleGenerator, allModules, new Stack<string>());

            var resolver = new Resolver(null, loader, errors =>
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

            foreach (var module in allModules.Values)
            {
                // Request entry-point bindings
                var addTo = module.IsOverride ? overrides : bindings;

                foreach (var injectType in module.Injects)
                {
                    var key = injectType.Resolve().IsInterface
                                  ? CompilerKeys.ForType(injectType)
                                  : CompilerKeys.GetMemberKey(injectType);

                    resolver.RequestBinding(key, module.ModuleType.FullName, false, true);
                }

                foreach (var providerGenerator in module.ProviderGenerators)
                {

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

            return !hasError ? allBindings : null;
        }

        private void GatherIncludedModules(
            ModuleGenerator module,
            IDictionary<string, ModuleGenerator> modules,
            Stack<string> path)
        {
            var name = module.ModuleType.FullName;

            if (path.Contains(name))
            {
                var sb = new StringBuilder("Circular module dependency: ");

                if (path.Count == 1)
                {
                    sb.AppendFormat("{0} includes itself directly.", name);
                }
                else
                {
                    var includer = name;
                    for (var i = 0; path.Count > 0; ++i)
                    {
                        var current = includer;
                        includer = path.Pop();
                        sb.AppendLine()
                          .AppendFormat("{0}.  {1} included by {2}", i, current, includer);
                    }
                }

                throw new ValidationException(sb.ToString());
            }

            modules.Add(name, module);

            foreach (var typeReference in module.IncludedModules)
            {
                path.Push(name);
                GatherIncludedModules(modulesByTypeName[typeReference.FullName], modules, path);
                path.Pop();
            }
        }

        private string PrepareGraphvizDirectory(string projectDirectory)
        {
            var graphvizDirectory = Path.Combine(projectDirectory, "graphviz");

            if (!Directory.Exists(graphvizDirectory))
            {
                Directory.CreateDirectory(graphvizDirectory);
            }
            else
            {
                foreach (var dotFile in Directory.EnumerateFiles(
                    projectDirectory,
                    "*.dot",
                    SearchOption.TopDirectoryOnly))
                {
                    File.Delete(dotFile);
                }
            }

            return graphvizDirectory;
        }

        private void WriteModuleGraph(
            ModuleGenerator completeModule,
            IDictionary<string, Binding> allBindings,
            string graphvizDirectory)
        {
            var safeModuleName = completeModule.ModuleType.FullName.Replace('/', '.');
            var fileName = Path.Combine(graphvizDirectory, safeModuleName + ".dot");
            using (var fs = File.Open(fileName, FileMode.Create, FileAccess.Write))
            using (var dotWriter = new DotWriter(fs))
            {
                new GraphWriter().Write(dotWriter, allBindings);
            }
        }
    }
}
