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
using System.Collections.Generic;
﻿using System.IO;
﻿using System.Linq;
﻿using System.Xml.Linq;
﻿using Stiletto.Fody.Validation;
﻿using Stiletto.Internal.Plugins.Codegen;
﻿using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stiletto.Fody
{
    public class ModuleWeaver
    {
        private ErrorReporter errorReporter;
        private IDictionary<string, Tuple<AssemblyDefinition, bool>> dependencies;

        public bool HasError { get { return errorReporter.HasError; } }

        #region Fody-provided members

        public XElement Config { get; set; }
        public ModuleDefinition ModuleDefinition { get; set; }

        public Action<string> LogInfo { get; set; }
        public Action<string> LogWarning { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string, SequencePoint> LogWarningPoint { get; set; }
        public Action<string, SequencePoint> LogErrorPoint { get; set; }

        public List<string> ReferenceCopyLocalPaths { get; set; }

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

            var processors = GatherModulesNeedingProcessing();

            foreach (var p in processors) {
                p.CreateGenerators();
                p.ValidateGenerators();
            }

            if (HasError) {
                return;
            }

            ValidateCompleteGraph(processors);

            if (HasError) {
                return;
            }

            foreach (var p in processors) {
                p.GenerateAdapters();
            }

            if (HasError) {
                return;
            }

            var pluginCtors = processors.Select(p => p.CompiledPluginConstructor).ToList();

            foreach (var p in processors) {
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

        private void ValidateCompleteGraph(IList<ModuleProcessor> processors)
        {
            var allModules = processors.SelectMany(p => p.ModuleGenerators);
            var allInjects = processors.SelectMany(p => p.InjectGenerators);
            var allLazys = processors.SelectMany(p => p.LazyGenerators);
            var allProvides = processors.SelectMany(p => p.ProviderGenerators);
            new Validator(errorReporter, allInjects, allLazys, allProvides, allModules).ValidateCompleteModules();
        }

        private IList<ModuleProcessor> GatherModulesNeedingProcessing()
        {
            var processors = new List<ModuleProcessor>();

            dependencies = new Dictionary<string, Tuple<AssemblyDefinition, bool>>();
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

            // Load assemblies and yield ModuleProcessors.
            foreach (var pathAndHasPdb in copyLocalAssemblies)
            {
                var path = pathAndHasPdb.Key;
                var hasPdb = pathAndHasPdb.Value;
                var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = hasPdb });

                dependencies[path] = Tuple.Create(assembly, hasPdb);

                foreach (var module in assembly.Modules) {
                    if (!IsModuleProcessable(module)) {
                        continue;
                    }

                    processors.Add(new ModuleProcessor(errorReporter, module));
                }
            }

            if (IsModuleProcessable(ModuleDefinition)) {
                processors.Insert(0, new ModuleProcessor(errorReporter, ModuleDefinition));
            }

            return processors;
        }

        /// <summary>
        /// Prepares the weaving environment.
        /// </summary>
        private void Initialize()
        {
            LogWarning = LogWarning ?? Console.WriteLine;
            LogError = LogError ?? Console.WriteLine;
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
            if (module.CustomAttributes.Any(Attributes.IsProcessedAssemblyAttribute)) {
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

    internal class AssemblyComparer : IEqualityComparer<AssemblyDefinition>
    {
        public bool Equals(AssemblyDefinition x, AssemblyDefinition y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            return x.FullName.Equals(y.FullName, StringComparison.Ordinal);
        }

        public int GetHashCode(AssemblyDefinition obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            return obj.FullName.GetHashCode();
        }
    }
}
