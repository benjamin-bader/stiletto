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
﻿using System.Reflection;
﻿using System.Xml.Linq;
using Abra.Fody.Generators;
﻿using Abra.Fody.Validation;
﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Abra.Fody
{
    public class ModuleWeaver
    {
        private ErrorReporter errorReporter;

        public bool IsPrimary { get; set; }
        public bool HasError { get { return errorReporter.HasError; } }
        public References References { get; set; }

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

        public MethodReference GeneratedPluginConstructor { get; private set; }
        public IList<ModuleGenerator> GeneratedModules { get; private set; }

        private List<MethodReference> subweaverPluginConstructors = new List<MethodReference>();
        private List<ModuleGenerator> subweaverModules = new List<ModuleGenerator>();

        public ModuleWeaver()
            : this(true, null)
        {
        }

        private ModuleWeaver(bool isPrimary, ErrorReporter errorReporter)
        {
            IsPrimary = isPrimary;
            this.errorReporter = errorReporter ?? new ErrorReporter(this);
            GeneratedModules = new List<ModuleGenerator>();
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
        /// Generate an <see cref="Abra.Internal.IPlugin"/> implementation containing the generated adapters
        /// Rewrite all Container.Create invocations in the module with Container.CreateWithPlugin invocations, using the generated plugin.
        /// </list>
        /// </remarks>
        public void Execute()
        {
            References = new References(ModuleDefinition);

            // Step 1: Make sure we haven't already processed this module.
            if (!EnsureModuleIsProcessable()) {
                return;
            }

            ForkSubsidiaryWeaversIfPrimary();

            // Step 2: Discover and validate all injectable elements.  These are:
            //         1. Types containing [Inject] members
            //         2. Types with a [Module] attribute
            //         3. [Inject] members of type System.Lazy<T>
            //         4. [Inject] members of type IProvider<T>
            var moduleTypes = new List<TypeDefinition>();
            var injectTypes = new List<TypeDefinition>();

            foreach (var t in ModuleDefinition.GetTypes()) {
                if (IsModule(t)) {
                    moduleTypes.Add(t);
                } else if (IsInject(t)) {
                    injectTypes.Add(t);
                }
            }

            var moduleGenerators = moduleTypes.Select(m => new ModuleGenerator(ModuleDefinition, References, m)).ToList();
            var injectGenerators = GatherInjectBindings(
                injectTypes,
                moduleGenerators.SelectMany(m => m.EntryPoints));

            var generators = new Queue<Generator>();
            foreach (var m in moduleGenerators) {
                m.Validate(errorReporter);
                generators.Enqueue(m);
            }

            foreach (var i in injectGenerators) {
                i.Validate(errorReporter);
                generators.Enqueue(i);
            }

            IList<LazyBindingGenerator> lazyGenerators;
            IList<ProviderBindingGenerator> providerGenerators;
            GetherParameterizedBindings(injectGenerators, out lazyGenerators, out providerGenerators);

            foreach (var g in lazyGenerators) {
                g.Validate(errorReporter);
                generators.Enqueue(g);
            }

            foreach (var g in providerGenerators) {
                g.Validate(errorReporter);
                generators.Enqueue(g);
            }

            if (errorReporter.HasError) {
                return;
            }

            // Step 3: Now that we know individual elements are all valid, validate the object graph as a whole.
            new Validator(errorReporter, injectGenerators, lazyGenerators, providerGenerators, moduleGenerators.Concat(subweaverModules))
                .ValidateCompleteModules();

            if (errorReporter.HasError) {
                return;
            }

            // Step 4: The graph is valid, emit generated adapters.
            var generatedTypes = new HashSet<TypeDefinition>(new TypeReferenceComparer());
            while (generators.Count > 0) {
                var current = generators.Dequeue();
                var newType = current.Generate(errorReporter);

                if (!generatedTypes.Add(newType)) {
                    continue;
                }

                if (newType.DeclaringType != null) {
                    newType.DeclaringType.NestedTypes.Add(newType);
                } else {
                    ModuleDefinition.Types.Add(newType);
                }
            }

            if (errorReporter.HasError) {
                return;
            }

            // Step 5: Emit a plugin that uses the generated adapters[
            var pluginGenerator = new PluginGenerator(
                ModuleDefinition,
                References,
                injectGenerators.Select(gen => gen.GetKeyedCtor()),
                lazyGenerators.Select(gen => gen.GetKeyedCtor()),
                providerGenerators.Select(gen => gen.GetKeyedCtor()),
                moduleGenerators.Select(gen => gen.GetModuleTypeAndGeneratedCtor()));

            ModuleDefinition.Types.Add(pluginGenerator.Generate(errorReporter));
            
            subweaverPluginConstructors.Insert(0, pluginGenerator.GeneratedCtor);

            if (errorReporter.HasError) {
                return;
            }

            // Step 6: Rewrite uses of Container to use the generated plugin.
            foreach (var method in GetContainerCreateInvocations()) {
                RewriteContainerCreateInvocations(method);
            }

            GeneratedPluginConstructor = pluginGenerator.GeneratedCtor;
            GeneratedModules = moduleGenerators;

            // Done!
        }

        private IList<InjectBindingGenerator> GatherInjectBindings(
            IEnumerable<TypeDefinition> injectTypes,
            IEnumerable<TypeReference> entryPoints)
        {
            var internalInjectTypes = new HashSet<TypeReference>(injectTypes, new TypeReferenceComparer());
            var injectGenerators = new List<InjectBindingGenerator>();

            foreach (var e in entryPoints) {
                if (internalInjectTypes.Contains(e)) {
                    internalInjectTypes.Remove(e);
                }

                injectGenerators.Add(new InjectBindingGenerator(ModuleDefinition, References, e, true));
            }

            injectGenerators.AddRange(internalInjectTypes.Select(i => new InjectBindingGenerator(ModuleDefinition, References, i, false)));

            return injectGenerators;
        }

        private void GetherParameterizedBindings(
            IEnumerable<InjectBindingGenerator> injectBindings,
            out IList<LazyBindingGenerator> lazyBindings,
            out IList<ProviderBindingGenerator> providerBindings)
        {
            lazyBindings = new List<LazyBindingGenerator>();
            providerBindings = new List<ProviderBindingGenerator>();

            foreach (var inject in injectBindings) {
                LazyBindingGenerator lazyGenerator;
                ProviderBindingGenerator providerGenerator;

                foreach (var param in inject.CtorParams) {
                    if (TryGetLazyBinding(param, inject.InjectedType, "Constructor parameter", out lazyGenerator)) {
                        lazyBindings.Add(lazyGenerator);
                    }

                    if (TryGetProviderBinding(param, inject.InjectedType, "Constructor parameter", out providerGenerator)) {
                        providerBindings.Add(providerGenerator);
                    }
                }

                foreach (var prop in inject.InjectableProperties) {
                    if (TryGetLazyBinding(prop, inject.InjectedType, "Property", out lazyGenerator)) {
                        lazyBindings.Add(lazyGenerator);
                    }

                    if (TryGetProviderBinding(prop, inject.InjectedType, "Property", out providerGenerator)) {
                        providerBindings.Add(providerGenerator);
                    }
                }
            }
        }

        private void ForkSubsidiaryWeaversIfPrimary()
        {
            if (!IsPrimary) {
                return;
            }

            Initialize();

            var copyLocalAssemblies = new Dictionary<string, bool>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (var copyLocal in ReferenceCopyLocalPaths) {
                if (copyLocal.EndsWith(".pdb") || copyLocal.EndsWith(".mdb")) {
                    queue.Enqueue(copyLocal);
                    continue;
                }

                if (copyLocal.EndsWith(".exe") || copyLocal.EndsWith(".dll")) {
                    copyLocalAssemblies[copyLocal] = false;
                }
            }

            while (queue.Count > 0) {
                var pdb = queue.Dequeue();
                var rawPath = Path.Combine(Path.GetDirectoryName(pdb), Path.GetFileNameWithoutExtension(pdb));
                var dll = rawPath + ".dll";
                var exe = rawPath + ".exe";
                
                if (copyLocalAssemblies.ContainsKey(dll)) {
                    copyLocalAssemblies[dll] = true;
                }

                if (copyLocalAssemblies.ContainsKey(exe)) {
                    copyLocalAssemblies[exe] = true;
                }
            }

            foreach (var pathAndHasPdb in copyLocalAssemblies) {
                var path = pathAndHasPdb.Key;
                var hasPdb = pathAndHasPdb.Value;
                var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {ReadSymbols = hasPdb});

                foreach (var module in assembly.Modules) {
                    var subWeaver = new ModuleWeaver(false, errorReporter)
                                        {
                                            LogWarning = LogWarning,
                                            LogError = LogError,
                                            ModuleDefinition = module
                                        };

                    subWeaver.Execute();

                    if (subWeaver.HasError) {
                        return;
                    }

                    subweaverPluginConstructors.Add(ModuleDefinition.Import(subWeaver.GeneratedPluginConstructor));
                    subweaverModules.AddRange(subWeaver.GeneratedModules);
                }

                assembly.Write(path, new WriterParameters {WriteSymbols = hasPdb});
            }
        }

        private bool TryGetProviderBinding(InjectMemberInfo injectMemberInfo, TypeDefinition containingType, string memberTypeName, out ProviderBindingGenerator generator)
        {
            return TryGetParameterizedBinding(
                injectMemberInfo,
                containingType,
                memberTypeName,
                "IProvider<T>",
                imi => imi.HasProviderKey,
                (imi, t) => new ProviderBindingGenerator(ModuleDefinition, References, imi.Key, imi.ProviderKey, t),
                out generator);
        }

        private bool TryGetLazyBinding(InjectMemberInfo injectMemberInfo, TypeReference containingType, string memberTypeName, out LazyBindingGenerator generator)
        {
            return TryGetParameterizedBinding(
                injectMemberInfo,
                containingType,
                memberTypeName,
                "Lazy<T>",
                imi => imi.HasLazyKey,
                (imi, t) => new LazyBindingGenerator(ModuleDefinition, References, imi.Key, imi.LazyKey, t),
                out generator);
        }

        private bool TryGetParameterizedBinding<TGenerator>(
            InjectMemberInfo injectMemberInfo,
            TypeReference containingType,
            string memberTypeName,
            string providedTypeName,
            Predicate<InjectMemberInfo> isParameterizedBinding,
            Func<InjectMemberInfo, TypeReference, TGenerator> selector,
            out TGenerator generator)
        {
            generator = default(TGenerator);

            if (!isParameterizedBinding(injectMemberInfo)) {
                return false;
            }

            var memberType = injectMemberInfo.Type as GenericInstanceType;
            if (memberType == null || memberType.GenericArguments.Count != 1)
            {
                var error = string.Format(
                    "{0} '{1}' of type '{2}' was detected as '{3}' but is actually a '{4}'; please report this as a bug.",
                    memberTypeName,
                    injectMemberInfo.MemberName,
                    containingType.FullName,
                    providedTypeName,
                    injectMemberInfo.Type.FullName);
                errorReporter.LogError(error);
                return false;
            }

            generator = selector(injectMemberInfo, memberType.GenericArguments[0]);

            return true;
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
        private bool EnsureModuleIsProcessable()
        {
            if (ModuleDefinition.CustomAttributes.Any(Attributes.IsProcessedAssemblyAttribute)) {
                LogWarning("This assembly has already had injectors generated, will not continue.");
                return false;
            }

            ModuleDefinition.CustomAttributes.Add(new CustomAttribute(References.ProcessedAssemblyAttribute_Ctor));
            return true;
        }

        /// <summary>
        /// Checks if a given <paramref name="type"/> is a module.
        /// </summary>
        /// <remarks>
        /// To be a module, a type must be decorated with a [Module] attribute.
        /// </remarks>
        /// <param name="type">
        /// The possible module.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the give <paramref name="type"/>
        /// is a module, and <see langword="false"/> otherwise.
        /// </returns>
        private static bool IsModule(TypeDefinition type)
        {
            return type.HasCustomAttributes
                && type.CustomAttributes.Any(Attributes.IsModuleAttribute);
        }

        /// <summary>
        /// Checks if a given <paramref name="type"/> is injectable.
        /// </summary>
        /// <remarks>
        /// To be "injectable", a type needs to have at least one property
        /// or constructor decorated with an [Inject] attribute.
        /// </remarks>
        /// <param name="type">
        /// The possibly-injectable type.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the given <paramref name="type"/>
        /// is injectable, and <see langword="false"/> otherwise.
        /// </returns>
        private static bool IsInject(TypeDefinition type)
        {
            return type.GetConstructors().Any(c => c.CustomAttributes.Any(Attributes.IsInjectAttribute))
                || type.Properties.Any(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute));
        }
         
        /// <summary>
        /// Gets all method definitions in the current module that contain calls
        /// to <see cref="Container.Create"/>.
        /// </summary>
        private IEnumerable<MethodDefinition> GetContainerCreateInvocations()
        {
            return from t in ModuleDefinition.GetTypes()
                   from m in t.Methods
                   where m.HasBody
                   let instrs = m.Body.Instructions
                   where instrs.Any(i => i.OpCode == OpCodes.Call
                                      && i.Operand is MethodReference
                                      && ((MethodReference)i.Operand).AreSame(References.Container_Create))
                   select m;
        }

        /// <summary>
        /// Replaces all invocations of <see cref="Container.Create"/> with a
        /// call to <see cref="Container.CreateWithPlugin"/> using the given
        /// generated plugin.
        /// </summary>
        /// <param name="method">
        /// The method whose container creations are to be rewritten.
        /// </param>
        private void RewriteContainerCreateInvocations(MethodDefinition method)
        {
            if (!method.HasBody) {
                return;
            }

            VariableDefinition pluginsArray = null;
            for (var instr = method.Body.Instructions.First(); instr != null; instr = instr.Next) {
                if (instr.OpCode != OpCodes.Call && instr.OpCode != OpCodes.Callvirt) {
                    continue;
                }

                var methodReference = (MethodReference) instr.Operand;

                if (!methodReference.AreSame(References.Container_Create)) {
                    continue;
                }

                if (pluginsArray == null) {
                    pluginsArray = new VariableDefinition(
                        "plugins",
                        ModuleDefinition.Import(new ArrayType(References.IPlugin)));
                    method.Body.Variables.Add(pluginsArray);
                    method.Body.InitLocals = true;
                }

                // Container.Create(object[]) -> Container.CreateWithPlugin(object[], IPlugin);
                var instrs = new List<Instruction>();
                instrs.Add(Instruction.Create(OpCodes.Ldc_I4, subweaverPluginConstructors.Count));
                instrs.Add(Instruction.Create(OpCodes.Newarr, References.IPlugin));
                instrs.Add(Instruction.Create(OpCodes.Stloc, pluginsArray));

                for (var i = 0; i < subweaverPluginConstructors.Count; ++i) {
                    instrs.Add(Instruction.Create(OpCodes.Ldloc, pluginsArray));
                    instrs.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                    instrs.Add(Instruction.Create(OpCodes.Newobj, subweaverPluginConstructors[i]));
                    instrs.Add(Instruction.Create(OpCodes.Stelem_Ref));
                }

                instrs.Add(Instruction.Create(OpCodes.Ldloc, pluginsArray));

                var il = method.Body.GetILProcessor();
                foreach (var instruction in instrs) {
                    il.InsertBefore(instr, instruction);
                }

                instr.Operand = References.Container_CreateWithPlugins;
            }
        }

        private class TypeReferenceComparer : IEqualityComparer<TypeReference>
        {
            public bool Equals(TypeReference x, TypeReference y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;

                return x.FullName.Equals(y.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(TypeReference obj)
            {
                return obj.FullName.GetHashCode();
            }
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
