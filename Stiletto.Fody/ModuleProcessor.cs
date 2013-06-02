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

        public MethodReference CompiledPluginConstructor { get; private set; }
        public bool UsesStiletto { get; private set; }

        public ModuleProcessor(IErrorReporter errorReporter, ModuleDefinition moduleDefinition, StilettoReferences stilettoReferences)
        {
            this.errorReporter = Conditions.CheckNotNull(errorReporter, "errorReporter");
            this.moduleDefinition = Conditions.CheckNotNull(moduleDefinition, "moduleDefinition");

            references = new References(moduleDefinition, stilettoReferences);
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

            foreach (var t in moduleDefinition.GetTypes()) {
                if (IsModule(t)) {
                    moduleTypes.Add(t);
                } else if (IsInject(t)) {
                    injectTypes.Add(t);
                }
            }

            moduleGenerators.AddRange(moduleTypes
                .Select(m => new ModuleGenerator(moduleDefinition, references, m)));

            GatherInjectBindings(
                injectTypes,
                moduleGenerators.SelectMany(m => m.EntryPoints));

            foreach (var g in injectGenerators) {
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
            foreach (var g in moduleGenerators) {
                g.Validate(errorReporter);
            }

            // Inject bindings are already validated

            foreach (var g in lazyGenerators) {
                g.Validate(errorReporter);
            }

            foreach (var g in providerGenerators) {
                g.Validate(errorReporter);
            }
        }

        public void GenerateAdapters()
        {
            // Step 4: The graph is valid, emit generated adapters.
            var generatedTypes = new HashSet<TypeDefinition>(new TypeReferenceComparer());
            var generators = new Queue<Generator>();

            foreach (var g in moduleGenerators) {
                generators.Enqueue(g);
            }

            foreach (var g in injectGenerators) {
                generators.Enqueue(g);
            }

            foreach (var g in lazyGenerators) {
                generators.Enqueue(g);
            }

            foreach (var g in providerGenerators) {
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

            var pluginGenerator = new PluginGenerator(
                moduleDefinition,
                references,
                injectGenerators.Where(gen => gen.IsVisibleToPlugin).Select(gen => gen.GetKeyedCtor()).Where(ctor => ctor != null),
                lazyGenerators.Select(gen => gen.GetKeyedCtor()),
                providerGenerators.Select(gen => gen.GetKeyedCtor()),
                moduleGenerators.Where(gen => gen.IsVisibleToPlugin).Select(gen => gen.GetModuleTypeAndGeneratedCtor()));

            moduleDefinition.Types.Add(pluginGenerator.Generate(errorReporter));

            CompiledPluginConstructor = pluginGenerator.GeneratedCtor;

            moduleDefinition.CustomAttributes.Add(new CustomAttribute(references.ProcessedAssemblyAttribute_Ctor));
        }

        /// <summary>
        /// Replaces all invocations of <see cref="Container.Create"/> with a
        /// call to <see cref="Container.CreateWithPlugins"/> using the given
        /// generated plugins.
        /// </summary>
        /// <param name="pluginCtors">
        /// A list of IPlugin constructors to be invoked, the results of which
        /// will be passed to <see cref="Container.CreateWithPlugins"/>.
        /// </param>
        public void RewriteContainerCreateInvocations(IList<MethodReference> pluginCtors)
        {
            var methods = from t in moduleDefinition.GetTypes()
                          from m in t.Methods
                          where m.HasBody
                          let instrs = m.Body.Instructions
                          where instrs.Any(i => i.OpCode == OpCodes.Call
                                             && i.Operand is MethodReference
                                             && ((MethodReference)i.Operand).AreSame(references.Container_Create))
                          select m;

            var arrayOfIPlugin = moduleDefinition.Import(new ArrayType(references.IPlugin));

            foreach (var method in methods)
            {
                VariableDefinition pluginsArray = null;
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

                    if (pluginsArray == null)
                    {
                        pluginsArray = new VariableDefinition(
                            "plugins",
                            arrayOfIPlugin);
                        method.Body.Variables.Add(pluginsArray);
                        method.Body.InitLocals = true;
                    }

                    // Container.Create(object[]) -> Container.CreateWithPlugins(object[], IPlugin[]);
                    var instrs = new List<Instruction>();
                    instrs.Add(Instruction.Create(OpCodes.Ldc_I4, pluginCtors.Count));
                    instrs.Add(Instruction.Create(OpCodes.Newarr, references.IPlugin));
                    instrs.Add(Instruction.Create(OpCodes.Stloc, pluginsArray));

                    for (var i = 0; i < pluginCtors.Count; ++i)
                    {
                        instrs.Add(Instruction.Create(OpCodes.Ldloc, pluginsArray));
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                        instrs.Add(Instruction.Create(OpCodes.Newobj, moduleDefinition.Import(pluginCtors[i])));
                        instrs.Add(Instruction.Create(OpCodes.Stelem_Ref));
                    }

                    instrs.Add(Instruction.Create(OpCodes.Ldloc, pluginsArray));

                    var il = method.Body.GetILProcessor();
                    foreach (var instruction in instrs)
                    {
                        il.InsertBefore(instr, instruction);
                    }

                    instr.Operand = references.Container_CreateWithPlugins;
                }
            }
        }

        private void GatherInjectBindings(IEnumerable<TypeDefinition> injectTypes, IEnumerable<TypeReference> entryPoints)
        {
            var injectTypeSet = new HashSet<TypeReference>(injectTypes, new TypeReferenceComparer());

            foreach (var entryPoint in entryPoints)
            {
                injectTypeSet.Remove(entryPoint);
                injectGenerators.Add(new InjectBindingGenerator(moduleDefinition, references, entryPoint, true));
            }

            injectGenerators.AddRange(injectTypeSet.Select(i =>
                new InjectBindingGenerator(moduleDefinition, references, i, false)));
        }

        private void GatherParameterizedBindings()
        {
            foreach (var inject in injectGenerators) {
                LazyBindingGenerator lazyGenerator;
                ProviderBindingGenerator providerGenerator;

                foreach (var param in inject.CtorParams) {
                    if (TryGetLazyGenerator(param, inject.InjectedType, "Constructor parameter", out lazyGenerator)) {
                        lazyGenerators.Add(lazyGenerator);
                    }

                    if (TryGetProviderGenerator(param, inject.InjectedType, "Constructor parameter", out providerGenerator)) {
                        providerGenerators.Add(providerGenerator);
                    }
                }

                foreach (var prop in inject.InjectableProperties) {
                    if (TryGetLazyGenerator(prop, inject.InjectedType, "Property", out lazyGenerator)) {
                        lazyGenerators.Add(lazyGenerator);
                    }

                    if (TryGetProviderGenerator(prop, inject.InjectedType, "Property", out providerGenerator)) {
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
    }
}
