using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Abra.Fody.Generators;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Abra.Fody
{
    public class ModuleWeaver : IWeaver
    {
        public XElement Config { get; set; }
        public ModuleDefinition ModuleDefinition { get; set; }

        public Action<string> LogInfo { get; set; }
        public Action<string> LogWarning { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string, SequencePoint> LogWarningPoint { get; set; }
        public Action<string, SequencePoint> LogErrorPoint { get; set; }

        private IList<TypeDefinition> moduleTypes = new List<TypeDefinition>();
        private IList<TypeDefinition> injectTypes = new List<TypeDefinition>();
        private IList<TypeDefinition> providedTypes = new List<TypeDefinition>();
        private IList<TypeDefinition> lazyTypes = new List<TypeDefinition>();

        private bool hasError;

        private Queue<Generator> generators = new Queue<Generator>();

        void IWeaver.LogError(string message)
        {
            hasError = true;
            LogError(message);
        }

        void IWeaver.LogWarning(string message)
        {
            LogWarning(message);
        }

        public void Execute()
        {
            Initialize();

            foreach (var t in ModuleDefinition.GetTypes()) {
                if (IsModule(t)) {
                    moduleTypes.Add(t);
                } else if (IsInject(t)) {
                    injectTypes.Add(t);
                }
            }

            var moduleGenerators = moduleTypes.Select(m => new ModuleGenerator(ModuleDefinition, m)).ToList();

            var entryPointTypes = new HashSet<TypeReference>(
                moduleGenerators.SelectMany(m => m.EntryPoints),
                new TypeReferenceComparer());

            var injectGenerators = injectTypes
                .Select(i => new InjectBindingGenerator(ModuleDefinition, i, entryPointTypes.Contains(i)));

            foreach (var m in moduleGenerators) {
                m.Validate(this);
                generators.Enqueue(m);
            }

            foreach (var i in injectGenerators) {
                i.Validate(this);
                generators.Enqueue(i);
            }

            if (hasError) {
                return;
            }

            while (generators.Count > 0) {
                var current = generators.Dequeue();
                current.Generate(this);

            }

            // Get modules
            // Get injectables
            // Get providers
            // Generate module adapters
            // Generate inject bindings
            // Generate lazy+provider bindings
            // Generate a custom plugin

            // Find all Abra.Container.Create invocations
            // Replace them with a call to Container.CreateGenerated
        }

        public void EnqueueProviderBinding(TypeReference providedType)
        {
            var gen = new ProviderBindingGenerator(ModuleDefinition, providedType);
            gen.Validate(this);
            generators.Enqueue(gen);
        }

        public void EnqueueLazyBinding(TypeReference lazyType)
        {
            var gen = new LazyBindingGenerator(ModuleDefinition, lazyType);
            gen.Validate(this);
            generators.Enqueue(gen);
        }

        private void Initialize()
        {
            References.Initialize(ModuleDefinition);
        }

        private static bool IsModule(TypeDefinition type)
        {
            return type.HasCustomAttributes
                && type.CustomAttributes.Any(Attributes.IsModuleAttribute);
        }

        private static bool IsInject(TypeDefinition type)
        {
            return type.GetConstructors().Any(c => c.CustomAttributes.Any(Attributes.IsInjectAttribute))
                || type.Properties.Any(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute));
        }

        private IEnumerable<MethodDefinition> GetContainerCreateInvocations()
        {
            return from t in ModuleDefinition.Types
                   from m in t.Methods
                   where m.HasBody
                   let instrs = m.Body.Instructions
                   where instrs.Any(i => i.OpCode == OpCodes.Callvirt
                                      && i.Operand is MethodReference
                                      && ((MethodReference)i.Operand).AreSame(References.Container_Create))
                   select m;
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
