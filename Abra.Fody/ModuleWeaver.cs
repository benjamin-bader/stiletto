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

        private bool hasError;
        private IList<ProviderBindingGenerator> providerGenerators = new List<ProviderBindingGenerator>();
        private IList<LazyBindingGenerator> lazyGenerators = new List<LazyBindingGenerator>();

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

            var moduleTypes = new List<TypeDefinition>();
            var injectTypes = new List<TypeDefinition>();

            foreach (var t in ModuleDefinition.GetTypes()) {
                if (IsModule(t)) {
                    moduleTypes.Add(t);
                } else if (IsInject(t)) {
                    injectTypes.Add(t);
                } else if (PluginGenerator.GeneratedPluginName.Equals(t.Name, StringComparison.Ordinal)) {
                    LogWarning("This assembly has already had injectors generated, will not continue.");
                    return;
                }
            }

            var internalInjectTypes = new HashSet<TypeReference>(injectTypes, new TypeReferenceComparer());

            var moduleGenerators = moduleTypes.Select(m => new ModuleGenerator(ModuleDefinition, m)).ToList();

            var entryPoints = from m in moduleGenerators
                              from e in m.EntryPoints
                              select e;

            var injectGenerators = new List<Generator>();
            foreach (var e in entryPoints) {
                if (internalInjectTypes.Contains(e)) {
                    internalInjectTypes.Remove(e);
                }

                injectGenerators.Add(new InjectBindingGenerator(ModuleDefinition, e, true));
            }

            foreach (var i in internalInjectTypes) {
                injectGenerators.Add(new InjectBindingGenerator(ModuleDefinition, i, false));
            }

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

            var generatedTypes = new HashSet<TypeDefinition>(new TypeReferenceComparer());
            while (generators.Count > 0) {
                var current = generators.Dequeue();
                var newType = current.Generate(this);

                if (!generatedTypes.Add(newType)) {
                    continue;
                }

                if (newType.DeclaringType != null) {
                    newType.DeclaringType.NestedTypes.Add(newType);
                } else {
                    ModuleDefinition.Types.Add(newType);
                }
            }

            generators = null;

            var pluginGenerator = new PluginGenerator(
                ModuleDefinition,
                injectGenerators.Select(gen => gen.GetKeyedCtor()),
                lazyGenerators.Select(gen => gen.GetKeyedCtor()),
                providerGenerators.Select(gen => gen.GetKeyedCtor()),
                moduleGenerators.Select(gen => gen.GetModuleTypeAndGeneratedCtor()));

            pluginGenerator.Validate(this);
            ModuleDefinition.Types.Add(pluginGenerator.Generate(this));

            foreach (var method in GetContainerCreateInvocations()) {
                RewriteContainerCreateInvocations(method, pluginGenerator.GeneratedCtor);
            }
        }

        public void EnqueueProviderBinding(string providerKey, TypeReference providedType)
        {
            var gen = new ProviderBindingGenerator(ModuleDefinition, providerKey, providedType);
            gen.Validate(this);
            providerGenerators.Add(gen);
            generators.Enqueue(gen);
        }

        public void EnqueueLazyBinding(string lazyKey, TypeReference lazyType)
        {
            var gen = new LazyBindingGenerator(ModuleDefinition, lazyKey, lazyType);
            gen.Validate(this);
            lazyGenerators.Add(gen);
            generators.Enqueue(gen);
        }

        private void Initialize()
        {
            LogWarning = LogWarning ?? Console.WriteLine;
            LogError = LogError ?? Console.WriteLine;
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
            return from t in ModuleDefinition.GetTypes()
                   from m in t.Methods
                   where m.HasBody
                   let instrs = m.Body.Instructions
                   where instrs.Any(i => i.OpCode == OpCodes.Call
                                      && i.Operand is MethodReference
                                      && ((MethodReference)i.Operand).AreSame(References.Container_Create))
                   select m;
        }

        private void RewriteContainerCreateInvocations(MethodDefinition method, MethodReference pluginCtor)
        {
            if (!method.HasBody) {
                return;
            }

            for (var instr = method.Body.Instructions.First(); instr != null; instr = instr.Next) {
                if (instr == null) {
                    break;
                }

                if (instr.OpCode != OpCodes.Call && instr.OpCode != OpCodes.Callvirt) {
                    continue;
                }

                var methodReference = (MethodReference) instr.Operand;

                if (!methodReference.AreSame(References.Container_Create)) {
                    continue;
                }

                // Container.Create(object[]) -> Container.CreateWithPlugin(object[], IPlugin);
                method.Body.GetILProcessor().InsertBefore(instr, Instruction.Create(OpCodes.Newobj, pluginCtor));
                instr.Operand = References.Container_CreateWithPlugin;
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
    }
}
