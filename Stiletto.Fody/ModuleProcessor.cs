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
using Stiletto.Fody.Generators;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Stiletto.Fody
{
    public class ModuleProcessor
    {
        private readonly IErrorReporter errorReporter;
        private readonly ModuleDefinition moduleDefinition;
        private readonly IEnumerable<ModuleReader> submodules;

        private readonly List<ModuleGenerator> moduleGenerators;
        private readonly List<InjectBindingGenerator> injectGenerators;
        private readonly List<LazyBindingGenerator> lazyGenerators;
        private readonly List<ProviderBindingGenerator> providerGenerators;
        private readonly References references;
        private readonly Queue<TypeDefinition> baseGeneratorQueue;

        public IList<ModuleGenerator> ModuleGenerators
        {
            get { return moduleGenerators; }
        }

        public IList<InjectBindingGenerator> InjectGenerators
        {
            get { return injectGenerators; }
        }

        public IList<LazyBindingGenerator> LazyGenerators
        {
            get { return lazyGenerators; }
        }

        public IList<ProviderBindingGenerator> ProviderGenerators
        {
            get { return providerGenerators; }
        }

        public bool HasBaseTypesEnqueued
        {
            get { return baseGeneratorQueue.Count > 0; }
        }

        public MethodReference CompiledLoaderConstructor { get; private set; }
        public bool UsesStiletto { get; private set; }

        public ModuleProcessor(
            IErrorReporter errorReporter,
            ModuleDefinition moduleDefinition,
            References references,
            IEnumerable<ModuleReader> subModules)
        {
            this.errorReporter = Conditions.CheckNotNull(errorReporter, "errorReporter");
            this.moduleDefinition = Conditions.CheckNotNull(moduleDefinition, "moduleDefinition");
            this.references = references;
            this.submodules = subModules
                .Append(ModuleReader.Read(moduleDefinition))
                .Where(reader => reader.UsesStiletto);

            moduleGenerators = new List<ModuleGenerator>();
            injectGenerators = new List<InjectBindingGenerator>();
            lazyGenerators = new List<LazyBindingGenerator>();
            providerGenerators = new List<ProviderBindingGenerator>();
            baseGeneratorQueue = new Queue<TypeDefinition>();
        }

        public void EnqueueBaseType(TypeDefinition type)
        {
            baseGeneratorQueue.Enqueue(type);
        }

        public void CreateGenerators(ModuleWeaver weaver)
        {
            if (moduleDefinition.AssemblyReferences.All(reference => reference.Name != "Stiletto"))
            {
                UsesStiletto = false;
                return;
            }

            var moduleTypes = new List<TypeDefinition>();
            var injectTypes = new List<TypeDefinition>();

            foreach (var reader in submodules)
            {
                moduleTypes.AddRange(reader.ModuleTypes.Where(t => !weaver.ExcludedClasses.Contains(t.FullName)));
                injectTypes.AddRange(reader.InjectTypes.Where(t => !weaver.ExcludedClasses.Contains(t.FullName)));
            }

            moduleGenerators.AddRange(moduleTypes
                .Select(m => new ModuleGenerator(moduleDefinition, references, m)));

            GatherInjectBindings(
                injectTypes,
                moduleGenerators.SelectMany(m => m.Injects));

            foreach (var g in injectGenerators)
            {
                // We need to validate the inject binding generators to
                // discover their injectable constructors and properties,
                // which we need to have to get lazy and IProvider bindings.
                g.Weaver = weaver;
                g.Validate(errorReporter);
            }

            GatherParameterizedBindings();

            UsesStiletto = InjectGenerators.Any()
                        || ModuleGenerators.Any()
                        || LazyGenerators.Any()
                        || ProviderGenerators.Any();
        }

        /// <summary>
        /// Creates and validates any enqueued base-class generators that were enqueued.
        /// </summary>
        public void CreateBaseClassGenerators(ModuleWeaver weaver)
        {
            var injectedTypes = new HashSet<TypeDefinition>(InjectGenerators.Select(gen => gen.InjectedType));

            while (baseGeneratorQueue.Count > 0)
            {
                var typedef = baseGeneratorQueue.Dequeue();
                if (!injectedTypes.Add(typedef))
                {
                    continue;
                }

                var gen = new InjectBindingGenerator(moduleDefinition, references, typedef, false);
                gen.Weaver = weaver;
                gen.Validate(errorReporter);
                InjectGenerators.Add(gen);
            }
        }

        public void ValidateGenerators()
        {
            foreach (var g in moduleGenerators)
            {
                g.Validate(errorReporter);
            }

            // Inject bindings are already validated

            foreach (var g in lazyGenerators)
            {
                g.Validate(errorReporter);
            }

            foreach (var g in providerGenerators)
            {
                g.Validate(errorReporter);
            }
        }

        public void GetInjectableKeys(ISet<string> injectableKeys)
        {
            foreach (var g in injectGenerators)
            {
                if (!string.IsNullOrEmpty(g.Key))
                {
                    injectableKeys.Add(g.Key);
                }

                if (!string.IsNullOrEmpty(g.MembersKey))
                {
                    injectableKeys.Add(g.MembersKey);
                }
            }

            foreach (var g in moduleGenerators)
            {
                foreach (var p in g.ProviderGenerators)
                {
                    if (!string.IsNullOrEmpty(p.Key))
                    {
                        injectableKeys.Add(p.Key);
                    }
                }
            }

            foreach (var g in lazyGenerators)
            {
                if (!string.IsNullOrEmpty(g.Key))
                {
                    injectableKeys.Add(g.Key);
                }
            }

            foreach (var g in providerGenerators)
            {
                if (!string.IsNullOrEmpty(g.Key))
                {
                    injectableKeys.Add(g.Key);
                }
            }
        }

        public void ValidateCompleteModules(ISet<string> injectableKeys)
        {
            foreach (var g in moduleGenerators)
            {
                g.ValidateCompleteness(injectableKeys, errorReporter);
            }
        }

        public void GenerateAdapters()
        {
            // Step 4: The graph is valid, emit generated adapters.
            var generatedTypes = new HashSet<TypeDefinition>(new TypeReferenceComparer());
            var generators = new Queue<Generator>();

            foreach (var g in moduleGenerators)
            {
                generators.Enqueue(g);
            }

            foreach (var g in injectGenerators)
            {
                generators.Enqueue(g);
            }

            foreach (var g in lazyGenerators)
            {
                generators.Enqueue(g);
            }

            foreach (var g in providerGenerators)
            {
                generators.Enqueue(g);
            }

            while (generators.Count > 0)
            {
                var current = generators.Dequeue();
                var newType = current.Generate(errorReporter);

                // generators that are placeholders for non-injectable entry points (e.g. bool)
                // don't emit any types, so make sure we check that.
                if (newType == null)
                {
                    continue;
                }

                if (!generatedTypes.Add(newType))
                {
                    continue;
                }

                if (newType.DeclaringType != null)
                {
                    newType.DeclaringType.NestedTypes.Add(newType);
                }
                else
                {
                    moduleDefinition.Types.Add(newType);
                }
            }

            var loaderGenerator = new LoaderGenerator(
                moduleDefinition,
                references,
                injectGenerators.Where(gen => gen.IsVisibleToLoader).Select(gen => gen.GetKeyedCtor()).Where(ctor => ctor != null),
                lazyGenerators.Select(gen => gen.GetKeyedCtor()),
                providerGenerators.Select(gen => gen.GetKeyedCtor()),
                moduleGenerators.Where(gen => gen.IsVisibleToLoader).Select(gen => gen.GetModuleTypeAndGeneratedCtor()));

            moduleDefinition.Types.Add(loaderGenerator.Generate(errorReporter));

            CompiledLoaderConstructor = loaderGenerator.GeneratedCtor;

            moduleDefinition.CustomAttributes.Add(new CustomAttribute(references.ProcessedAssemblyAttribute_Ctor));
        }

        /// <summary>
        /// Replaces all invocations of <see cref="Container.Create"/> with a
        /// call to <see cref="Container.CreateWithLoaders"/> using the given
        /// generated loaders.
        /// </summary>
        /// <param name="loaderCtors">
        /// A list of ILoader constructors to be invoked, the results of which
        /// will be passed to <see cref="Container.CreateWithLoaders"/>.
        /// </param>
        public void RewriteContainerCreateInvocations(IList<MethodReference> loaderCtors)
        {
            var methods = from t in moduleDefinition.GetTypes()
                          from m in t.Methods
                          where m.HasBody
                          let instrs = m.Body.Instructions
                          where instrs.Any(i => i.OpCode == OpCodes.Call
                                             && i.Operand is MethodReference
                                             && ((MethodReference)i.Operand).AreSame(references.Container_Create))
                          select m;

            var arrayOfILoader = moduleDefinition.Import(new ArrayType(references.ILoader));

            foreach (var method in methods)
            {
                VariableDefinition loadersArray = null;
                for (var instr = method.Body.Instructions.First(); instr != null; instr = instr.Next)
                {
                    if (instr.OpCode != OpCodes.Call && instr.OpCode != OpCodes.Callvirt)
                    {
                        continue;
                    }

                    var methodReference = (MethodReference)instr.Operand;

                    if (!methodReference.AreSame(references.Container_Create))
                    {
                        continue;
                    }

                    if (loadersArray == null)
                    {
                        loadersArray = new VariableDefinition(
                            "loaders",
                            arrayOfILoader);
                        method.Body.Variables.Add(loadersArray);
                        method.Body.InitLocals = true;
                    }

                    // Container.Create(object[]) -> Container.CreateWithLoaders(object[], ILoader[]);
                    var instrs = new List<Instruction>();
                    instrs.Add(Instruction.Create(OpCodes.Ldc_I4, loaderCtors.Count));
                    instrs.Add(Instruction.Create(OpCodes.Newarr, references.ILoader));
                    instrs.Add(Instruction.Create(OpCodes.Stloc, loadersArray));

                    for (var i = 0; i < loaderCtors.Count; ++i)
                    {
                        instrs.Add(Instruction.Create(OpCodes.Ldloc, loadersArray));
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                        instrs.Add(Instruction.Create(OpCodes.Newobj, moduleDefinition.Import(loaderCtors[i])));
                        instrs.Add(Instruction.Create(OpCodes.Stelem_Ref));
                    }

                    instrs.Add(Instruction.Create(OpCodes.Ldloc, loadersArray));

                    var il = method.Body.GetILProcessor();
                    foreach (var instruction in instrs)
                    {
                        il.InsertBefore(instr, instruction);
                    }

                    instr.Operand = references.Container_CreateWithLoaders;
                }
            }
        }

        private void GatherInjectBindings(IEnumerable<TypeDefinition> injectTypes, IEnumerable<TypeReference> moduleInjectTypes)
        {
            var injectTypeSet = new HashSet<TypeReference>(injectTypes, new TypeReferenceComparer());

            foreach (var moduleInjectType in moduleInjectTypes)
            {
                injectTypeSet.Remove(moduleInjectType);
                injectGenerators.Add(new InjectBindingGenerator(moduleDefinition, references, moduleInjectType, true));
            }

            injectGenerators.AddRange(injectTypeSet.Select(i =>
                new InjectBindingGenerator(moduleDefinition, references, i, false)));
        }

        private void GatherParameterizedBindings()
        {
            foreach (var inject in injectGenerators)
            {
                LazyBindingGenerator lazyGenerator;
                ProviderBindingGenerator providerGenerator;

                foreach (var param in inject.CtorParams)
                {
                    if (TryGetLazyGenerator(param, inject.InjectedType, "Constructor parameter", out lazyGenerator))
                    {
                        lazyGenerators.Add(lazyGenerator);
                    }

                    if (TryGetProviderGenerator(param, inject.InjectedType, "Constructor parameter", out providerGenerator))
                    {
                        providerGenerators.Add(providerGenerator);
                    }
                }

                foreach (var prop in inject.InjectableProperties)
                {
                    if (TryGetLazyGenerator(prop, inject.InjectedType, "Property", out lazyGenerator))
                    {
                        lazyGenerators.Add(lazyGenerator);
                    }

                    if (TryGetProviderGenerator(prop, inject.InjectedType, "Property", out providerGenerator))
                    {
                        providerGenerators.Add(providerGenerator);
                    }
                }
            }
        }

        private bool TryGetProviderGenerator(InjectMemberInfo injectMemberInfo, TypeDefinition containingType, string memberTypeName, out ProviderBindingGenerator generator)
        {
            return TryGetParameterizedBinding(
                injectMemberInfo,
                containingType,
                memberTypeName,
                "IProvider<T>",
                imi => imi.HasProviderKey,
                (imi, t) => new ProviderBindingGenerator(moduleDefinition, references, imi.Key, imi.ProviderKey, t),
                out generator);
        }

        private bool TryGetLazyGenerator(InjectMemberInfo injectMemberInfo, TypeReference containingType, string memberTypeName, out LazyBindingGenerator generator)
        {
            return TryGetParameterizedBinding(
                injectMemberInfo,
                containingType,
                memberTypeName,
                "Lazy<T>",
                imi => imi.HasLazyKey,
                (imi, t) => new LazyBindingGenerator(moduleDefinition, references, imi.Key, imi.LazyKey, t),
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

            if (!isParameterizedBinding(injectMemberInfo))
            {
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
    }
}
