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
﻿using System.IO;
﻿using System.Linq;
﻿using System.Xml.Linq;
﻿using Stiletto.Fody.Validation;
﻿using Mono.Cecil;

namespace Stiletto.Fody
{
    public class ModuleWeaver
    {
        private readonly ErrorReporter errorReporter;
        private readonly IDictionary<string, Tuple<AssemblyDefinition, bool>> dependencies =
            new Dictionary<string, Tuple<AssemblyDefinition, bool>>();

        private readonly Dictionary<string, ModuleProcessor> modulesByAssembly =
            new Dictionary<string, ModuleProcessor>();

        private IList<ModuleProcessor> processors;

        private WeaverConfig weaverConfig;

        public bool HasError { get { return errorReporter.HasError; } }
        public Trie ExcludedClasses { get { return weaverConfig.ExcludedClassPatterns; } }

        #region Fody-provided members

        public XElement Config { get; set; }
        public ModuleDefinition ModuleDefinition { get; set; }
        public string ProjectDirectoryPath { get; set; }
        public Action<string> LogWarning { get; set; }
        public Action<string> LogError { get; set; }
        public List<string> ReferenceCopyLocalPaths { get; set; }
        public IAssemblyResolver AssemblyResolver { get; set; }

        #endregion

        public ModuleWeaver()
        {
            errorReporter = new ErrorReporter(this);
        }

        /// <summary>
        /// The entry point when invoked as part of the Fody pipeline.
        /// </summary>
        /// <remarks>
        /// The workflow here is:
        /// <list type="bullet">
        /// Verify that the module is processable
        /// Validate that injectable types and modules are individually valid as declared
        /// Validate the object graph that they represent, i.e. that complete modules have no unsatisfied dependencies
        /// Generate binding and module adapters
        /// Generate an <see cref="Stiletto.Internal.IPlugin"/> implementation containing the generated adapters
        /// Rewrite all Container.Create invocations in the module with Container.CreateWithPlugin invocations, using the generated plugin.
        /// </list>
        /// </remarks>
        public void Execute()
        {
            Initialize();

            processors = GatherModulesNeedingProcessing();

            foreach (var p in processors)
            {
                p.CreateGenerators(this);
            }

            // TODO: Moving to a single-assembly codegen target means that this is probably unnecessary now.  Figure that out.
            // Creating inject generators can trigger base-class binding generation
            // that crosses module or assembly boundaries; we need to resolve them here
            // prior to validating the graph.
            bool hasGeneratedBaseClasses;
            do
            {
                hasGeneratedBaseClasses = false;
                foreach (var p in processors)
                {
                    if (p.HasBaseTypesEnqueued)
                    {
                        p.CreateBaseClassGenerators(this);
                        hasGeneratedBaseClasses = true;
                    }
                }
            }
            while (hasGeneratedBaseClasses);

            processors = processors.Where(p => p.UsesStiletto).ToList();

            foreach (var p in processors)
            {
                p.ValidateGenerators();
            }

            if (HasError)
            {
                return;
            }

            ValidateCompleteGraph(processors);

            if (HasError)
            {
                return;
            }

            foreach (var p in processors)
            {
                p.GenerateAdapters();
            }

            if (HasError)
            {
                return;
            }

            var pluginCtors = processors.Select(p => p.CompiledPluginConstructor).ToList();

            foreach (var p in processors)
            {
                p.RewriteContainerCreateInvocations(pluginCtors);
            }

            foreach (var kvp in dependencies)
            {
                var path = kvp.Key;
                var assembly = kvp.Value.Item1;
                var hasPdb = kvp.Value.Item2;

                assembly.Write(path, new WriterParameters { WriteSymbols = hasPdb });
            }
        }

        public bool EnqueueBaseTypeBinding(TypeReference typeReference)
        {
            TypeDefinition typedef;

            BaseAssemblyResolver baseResolver = null;
            try
            {
                baseResolver = typeReference.Module.AssemblyResolver as BaseAssemblyResolver;
                if (baseResolver != null)
                {
                    // Sometimes the base resolver can't find assemblies that are copied
                    // locally.  Thanks to this handy event, we can try to resolve them
                    // ourselves - this has worked so far, but feels hacky.  Debugging
                    // Cecil + Fody is tricky, so root cause is unknown at present.
                    baseResolver.ResolveFailure += BaseResolverOnResolveFailure;
                }

                typedef = typeReference.Resolve();
            }
            catch (AssemblyResolutionException ex)
            {
                var format = "Failed to resolve type {0}: {1}";
                errorReporter.LogWarning(string.Format(format, typeReference.FullName, ex));
                return false;
            }
            finally
            {
                if (baseResolver != null)
                {
                    baseResolver.ResolveFailure -= BaseResolverOnResolveFailure;
                }
            }

            var processorKey = GetModuleKey(typedef.Module);

            if (!modulesByAssembly.ContainsKey(processorKey))
            {
                return false;
            }

            var usesStiletto =
                typedef.CustomAttributes.Any(Attributes.IsSingletonAttribute)
                || typedef.Properties.Any(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute))
                || typedef.Methods.Any(m => m.Name == ".ctor" && m.CustomAttributes.Any(Attributes.IsInjectAttribute));

            if (!usesStiletto)
            {
                return false;
            }

            processors.First().EnqueueBaseType(typedef);
            return true;
        }

        private AssemblyDefinition BaseResolverOnResolveFailure(object sender, AssemblyNameReference reference)
        {
            var warning = string.Format(
                "Default assembly resolver failed to resolve {0} in {1}, trying to resolve with Fody.",
                reference.FullName,
                Directory.GetCurrentDirectory());

            LogWarning(warning);
            return AssemblyResolver.Resolve(reference);
        }

        private void ValidateCompleteGraph(IList<ModuleProcessor> processors)
        {
            var allModules = processors.SelectMany(p => p.ModuleGenerators);
            var allInjects = processors.SelectMany(p => p.InjectGenerators);
            var allLazys = processors.SelectMany(p => p.LazyGenerators);
            var allProvides = processors.SelectMany(p => p.ProviderGenerators);
            new Validator(errorReporter, allInjects, allLazys, allProvides, allModules)
                .ValidateCompleteModules(
                    weaverConfig.SuppressUnusedBindingErrors,
                    weaverConfig.SuppressGraphviz,
                    ProjectDirectoryPath);
        }

        private IList<ModuleProcessor> GatherModulesNeedingProcessing()
        {
            var processors = new List<ModuleProcessor>();

            if (!IsModuleProcessable(ModuleDefinition))
            {
                return processors;
            }

            var moduleReaders = new List<ModuleReader>();
            var stilettoReferences = StilettoReferences.Create(AssemblyResolver);
            var references = new References(ModuleDefinition, stilettoReferences);

            var copyLocalAssemblies = new Dictionary<string, bool>(StringComparer.Ordinal);
            var localDebugFiles = new Queue<string>();

            foreach (var copyLocal in ReferenceCopyLocalPaths)
            {
                if (copyLocal.EndsWith(".pdb") || copyLocal.EndsWith(".mdb"))
                {
                    // We'll come back to the debug files after we have a complete
                    // list of local assemblies.
                    localDebugFiles.Enqueue(copyLocal);
                    continue;
                }

                if (copyLocal.EndsWith(".exe") || copyLocal.EndsWith(".dll"))
                {
                    copyLocalAssemblies[copyLocal] = false;
                }
            }

            // Check which assemblies have debug symbols and, consequently,
            // for which assemblies we will attempt to read and write such symbols.
            while (localDebugFiles.Count > 0)
            {
                var pdb = localDebugFiles.Dequeue();
                var rawPath = Path.Combine(Path.GetDirectoryName(pdb), Path.GetFileNameWithoutExtension(pdb));
                var dll = rawPath + ".dll";
                var exe = rawPath + ".exe";

                if (copyLocalAssemblies.ContainsKey(dll))
                {
                    copyLocalAssemblies[dll] = true;
                }

                if (copyLocalAssemblies.ContainsKey(exe))
                {
                    copyLocalAssemblies[exe] = true;
                }
            }

            foreach (var pathAndHasPdb in copyLocalAssemblies)
            {
                var path = pathAndHasPdb.Key;
                var hasPdb = pathAndHasPdb.Value;
                var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = hasPdb });

                // TODO: Figure out how to differentiate between third-party libs and client code.
                if (assembly.Name.HasPublicKey)
                {
                    LogWarning("Assembly " + assembly.Name + " is strong-named and will not be processed.");
                    continue;
                }

                dependencies[path] = Tuple.Create(assembly, hasPdb);

                foreach (var module in assembly.Modules)
                {
                    if (!IsModuleProcessable(module))
                    {
                        continue;
                    }

                    if (module.IsMain)
                    {
                        var importedCtor = module.Import(references.InternalsVisibleToAttribute);
                        var internalsVisibleTo = new CustomAttribute(importedCtor);
                        internalsVisibleTo.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, ModuleDefinition.Assembly.Name.Name));
                        module.Assembly.CustomAttributes.Add(internalsVisibleTo);
                    }

                    moduleReaders.Add(ModuleReader.Read(module));
                    /*var moduleProcessor = new ModuleProcessor(
                        errorReporter,
                        module,
                        new References(module, stilettoReferences));
                    processors.Add(moduleProcessor);*/
                    AddModuleToAssemblyDictionary(module, null);
                }
            }

            AddModuleToAssemblyDictionary(ModuleDefinition, null);
            var mainModuleProcessor = new ModuleProcessor(
                errorReporter,
                ModuleDefinition,
                references,
                moduleReaders);

            processors.Add(mainModuleProcessor);

            return processors;
        }

        private void AddModuleToAssemblyDictionary(ModuleDefinition module, ModuleProcessor moduleProcessor)
        {
            modulesByAssembly[GetModuleKey(module)] = moduleProcessor;
        }

        private static string GetModuleKey(ModuleDefinition moduleDefinition)
        {
            return moduleDefinition.Assembly.Name.FullName + "+" + moduleDefinition.Name;
        }

        /// <summary>
        /// Prepares the weaving environment.
        /// </summary>
        private void Initialize()
        {
            LogWarning = LogWarning ?? Console.WriteLine;
            LogError = LogError ?? Console.WriteLine;
            weaverConfig = WeaverConfig.Load(Config);
        }

        /// <summary>
        /// Checks the current module for the presence of a marker attribute.
        /// If the attribute is present, then the current module has already
        /// been processed by this weaver, and processing should halt. 
        /// </summary>
        /// <returns>
        /// Returns <see langword="true"/> if the module is processable, and
        /// <see langword="false"/> otherwise.
        /// </returns>
        private bool IsModuleProcessable(ModuleDefinition module)
        {
            if (module.CustomAttributes.Any(Attributes.IsProcessedAssemblyAttribute))
            {
                LogWarning("The module " + module.FullyQualifiedName + " has already been processed.");
                return false;
            }

            return true;
        }

        private class ErrorReporter : IErrorReporter
        {
            private readonly ModuleWeaver weaver;

            public bool HasError { get; private set; }

            public ErrorReporter(ModuleWeaver weaver)
            {
                this.weaver = weaver;
            }

            public void LogWarning(string message)
            {
                weaver.LogWarning(message);
            }

            public void LogError(string message)
            {
                weaver.LogError(message);
                HasError = true;
            }
        }
    }
}
