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
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;

using Abra.Internal.Plugins.Codegen;

namespace Abra.Fody.Generators
{
    public class PluginGenerator : Generator
    {
        public const string GeneratedPluginName = "$CompiledPlugin$";

        private readonly IEnumerable<KeyedCtor> injectBindingCtors;
        private readonly IEnumerable<KeyedCtor> lazyBindingCtors;
        private readonly IEnumerable<KeyedCtor> providerBindingCtors;
        private readonly IEnumerable<Tuple<TypeReference, MethodReference>> runtimeModuleCtors;
        private readonly MethodReference bindingFnCtor;
        private readonly MethodReference bindingFnInvoke;
        private readonly MethodReference lazyFnCtor;
        private readonly MethodReference lazyFnInvoke;
        private readonly MethodReference providerFnCtor;
        private readonly MethodReference providerFnInvoke;
        private readonly MethodReference moduleFnCtor;
        private readonly MethodReference moduleFnInvoke;

        private readonly TypeReference bindingFnType;
        private readonly TypeReference lazyFnType;
        private readonly TypeReference providerFnType;
        private readonly TypeReference moduleFnType;

        private TypeDefinition plugin;
        private FieldDefinition injectsField;
        private FieldDefinition lazyInjectsField;
        private FieldDefinition providersField;
        private FieldDefinition modulesField;
        private int factoryMethodsGenerated;

        public MethodReference GeneratedCtor { get; private set; }

        public PluginGenerator(
            ModuleDefinition moduleDefinition,
            References references,
            IEnumerable<KeyedCtor> injectBindingCtors,
            IEnumerable<KeyedCtor> lazyBindingCtors,
            IEnumerable<KeyedCtor> providerBindingCtors,
            IEnumerable<Tuple<TypeReference, MethodReference>> runtimeModuleCtors)
            : base(moduleDefinition, references)
        {
            var bindingFns = GetFnMethods(References.FuncOfT, References.Binding);
            var lazyFns = GetFnMethods(
                References.FuncOfT4,
                ModuleDefinition.TypeSystem.String,
                ModuleDefinition.TypeSystem.Object,
                ModuleDefinition.TypeSystem.String,
                References.Binding);
            var providerFns = GetFnMethods(
                References.FuncOfT5,
                ModuleDefinition.TypeSystem.String,
                ModuleDefinition.TypeSystem.Object,
                ModuleDefinition.TypeSystem.Boolean,
                ModuleDefinition.TypeSystem.String,
                References.Binding);
            var moduleFns = GetFnMethods(References.FuncOfT, References.RuntimeModule);

            bindingFnType = bindingFns.Item1;
            bindingFnCtor = bindingFns.Item2;
            bindingFnInvoke = bindingFns.Item3;

            lazyFnType = lazyFns.Item1;
            lazyFnCtor = lazyFns.Item2;
            lazyFnInvoke = lazyFns.Item3;

            providerFnType = providerFns.Item1;
            providerFnCtor = providerFns.Item2;
            providerFnInvoke = providerFns.Item3;

            moduleFnType = moduleFns.Item1;
            moduleFnCtor = moduleFns.Item2;
            moduleFnInvoke = moduleFns.Item3;

            this.injectBindingCtors = Conditions.CheckNotNull(injectBindingCtors, "injectBindingCtors");
            this.lazyBindingCtors = Conditions.CheckNotNull(lazyBindingCtors, "lazyBindingCtors");
            this.providerBindingCtors = Conditions.CheckNotNull(providerBindingCtors, "providerBindingCtors");
            this.runtimeModuleCtors = Conditions.CheckNotNull(runtimeModuleCtors, "runtimeModuleCtors");
        }

        private Tuple<TypeReference, MethodReference, MethodReference> GetFnMethods(
            TypeReference tFn,
            params TypeReference[] generics)
        {
            var t = Import(tFn.MakeGenericInstanceType(generics));

            var ctor = ImportGeneric(
                tFn,
                m => m.IsConstructor
                     && (m.Attributes & MethodAttributes.Public) == MethodAttributes.Public
                     && m.Parameters.Count == 2,
                generics);

            var invoke = ImportGeneric (
                tFn,
                m => m.Name == "Invoke",
                generics);

            return Tuple.Create(t, ctor, invoke);
        }

        public override void Validate(IErrorReporter errorReporter)
        {
        }

        /// <summary>
        /// Generates an IPlugin implementation that provides the given
        /// inject bindings, lazy bindings, provider bindings, and modules,
        /// at runtime.
        /// </summary>
        /// <remarks>
        /// The idea here is that we have a key and constructor methodref for all generated
        /// types; we can just wrap each methodref in a so-called factory function and maintain
        /// dictionaries of keys to factory Funcs; at runtime, either the proper Func is looked
        /// up or a KeyNotFoundException is thrown, passing the job off to other plugins.
        /// </remarks>
        public override TypeDefinition Generate(IErrorReporter errorReporter)
        {
            plugin = new TypeDefinition(
                CodegenPlugin.CompiledPluginNamespace,
                CodegenPlugin.CompiledPluginName,
                TypeAttributes.Public | TypeAttributes.Sealed,
                ModuleDefinition.TypeSystem.Object);

            plugin.Interfaces.Add (References.IPlugin);
            plugin.CustomAttributes.Add(new CustomAttribute(References.CompilerGeneratedAttribute));

            injectsField = new FieldDefinition("bindings", FieldAttributes.Private, References.DictionaryOfStringToBindingFn);
            lazyInjectsField = new FieldDefinition("lazyBindings", FieldAttributes.Private, References.DictionaryOfStringToLazyBindingFn);
            providersField = new FieldDefinition("providerBindings", FieldAttributes.Private, References.DictionaryOfStringToProviderBindingFn);
            modulesField = new FieldDefinition("modules", FieldAttributes.Private, References.DictionaryOfTypeToModuleFn);

            plugin.Fields.Add (injectsField);
            plugin.Fields.Add(lazyInjectsField);
            plugin.Fields.Add (providersField);
            plugin.Fields.Add (modulesField);

            EmitCtor();
            EmitGetInjectBinding();
            EmitGetLazyInjectBinding();
            EmitGetIProviderInjectBinding();
            EmitGetRuntimeModule();

            return plugin;
        }

        public override KeyedCtor GetKeyedCtor ()
        {
            return null;
        }

        private void EmitCtor()
        {
            /**
             * public $CompiledBinding$()
             *     : base()
             * {
             *     bindings = new Dictionary<string, Func<Binding>>(StringComparer.Ordinal);
             *     bindings.Add("key0-N", this.InjectBindingFactory_0-N);
             * 
             *     lazyBindings = new Dictionary<string, Func<string, object, string, Binding>>(StringComparer.Ordinal);
             *     lazyBindings.Add("lazyKey0-N", this.LazyInjectBindingFactory_0-N);
             * 
             *     providerBindings = new Dictionary<string, Func<string, object, bool, string, Binding>>(StringComparer.Ordinal);
             *     providerBindings.Add("providerKey0-N", this.ProviderBindingFactory_0-N);
             * 
             *     modules = new Dictionary<Type, Func<RuntimeModule>>();
             *     modules.Add(typeof(ModuleType0-N), this.ModuleFactory_0-N);
             * }
             */
            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                ModuleDefinition.TypeSystem.Void);

            var ordinalComparerGet = ModuleDefinition.Import (typeof(StringComparer).GetProperty("Ordinal").GetGetMethod());

            var il = ctor.Body.GetILProcessor();

            il.Emit (OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, ModuleDefinition.Import (ModuleDefinition.TypeSystem.Object.Resolve().GetConstructors().First()));

            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Call, ordinalComparerGet);
            il.Emit (OpCodes.Newobj, References.DictionaryOfStringToBindingFn_New);
            il.Emit (OpCodes.Stfld, injectsField);

            foreach (var keyedCtor in injectBindingCtors)
            {
                var factory = EmitInjectFactory(keyedCtor.Ctor);
                plugin.Methods.Add(factory);

                AddFactoryToDict(il, keyedCtor.Key, factory, injectsField, bindingFnCtor,
                                 References.DictionaryOfStringToBindingFn_Add);
            }

            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Call, ordinalComparerGet);
            il.Emit (OpCodes.Newobj, References.DictionaryOfStringToLazyBindingFn_New);
            il.Emit (OpCodes.Stfld, lazyInjectsField);

            foreach (var keyedCtor in lazyBindingCtors)
            {
                var factory = EmitLazyFactory(keyedCtor.Ctor);
                plugin.Methods.Add(factory);

                AddFactoryToDict(il, keyedCtor.Key, factory, lazyInjectsField, lazyFnCtor,
                                 References.DictionaryOfStringToLazyBindingFn_Add);
            }

            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Call, ordinalComparerGet);
            il.Emit (OpCodes.Newobj, References.DictionaryOfStringToProviderBindingFn_New);
            il.Emit (OpCodes.Stfld, providersField);

            foreach (var keyedCtor in providerBindingCtors)
            {
                var factory = EmitProviderFactory(keyedCtor.Ctor);
                plugin.Methods.Add(factory);

                AddFactoryToDict(il, keyedCtor.Key, factory, providersField, providerFnCtor,
                                 References.DictionaryOfStringToProviderBindingFn_Add);
            }

            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Newobj, References.DictionaryOfTypeToModuleFn_New);
            il.Emit (OpCodes.Stfld, modulesField);

            foreach (var tuple in runtimeModuleCtors)
            {
                var factory = EmitModuleFactory(tuple.Item2);
                plugin.Methods.Add(factory);

                // Different because we don't care about keys for modules, we can just dispatch on type.
                il.Emit (OpCodes.Ldarg_0);
                il.Emit (OpCodes.Ldfld, modulesField);
                il.Emit (OpCodes.Ldtoken, tuple.Item1);
                il.Emit (OpCodes.Call, References.Type_GetTypeFromHandle);
                il.Emit (OpCodes.Ldarg_0);
                il.Emit (OpCodes.Ldftn, factory);
                il.Emit (OpCodes.Newobj, moduleFnCtor);
                il.Emit (OpCodes.Call, References.DictionaryOfTypeToModuleFn_Add);
            }

            il.Emit (OpCodes.Ret);

            plugin.Methods.Add(ctor);
            GeneratedCtor = ctor;
        }

        private static void AddFactoryToDict(
            ILProcessor il,
            string key,
            MethodReference factory,
            FieldReference field,
            MethodReference fnCtor,
            MethodReference addFn)
        {
            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Ldfld, field);
            il.Emit (OpCodes.Ldstr, key);
            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Ldftn, factory);
            il.Emit (OpCodes.Newobj, fnCtor);
            il.Emit (OpCodes.Call, addFn);
        }

        private MethodDefinition EmitInjectFactory(MethodReference ctor)
        {
            /**
             * private Binding InjectBindingFactory_N()
             * {
             *     return new CompiledBindingN();
             * }
             */
            var factory = new MethodDefinition(
                "InjectBindingFactory_" + (factoryMethodsGenerated++),
                MethodAttributes.Private,
                References.Binding);

            var il = factory.Body.GetILProcessor();
            il.Emit (OpCodes.Newobj, ctor);
            il.Emit (OpCodes.Ret);

            return factory;
        }

        private MethodDefinition EmitLazyFactory(MethodReference ctor)
        {
            /**
             * private Binding LazyInjectBindingFactory_N(string key, object requiredBy, string lazyKey)
             * {
             *     return new CompiledLazyBinding(key, requiredBy, lazyKey);
             * }
             */
            var factory = new MethodDefinition(
                "LazyInjectBindingFactory_" + (factoryMethodsGenerated++),
                MethodAttributes.Private,
                References.Binding);

            factory.Parameters.Add (new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            factory.Parameters.Add (new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
            factory.Parameters.Add (new ParameterDefinition("lazyKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

            var il = factory.Body.GetILProcessor();
            il.Emit (OpCodes.Ldarg_1);
            il.Emit (OpCodes.Ldarg_2);
            il.Emit (OpCodes.Ldarg_3);
            il.Emit (OpCodes.Newobj, ctor);
            il.Emit (OpCodes.Ret);

            return factory;
        }

        private MethodDefinition EmitProviderFactory(MethodReference ctor)
        {
            /**
             * private Binding ProviderBindingFactory_N(string key, object requiredBy, bool mustBeInjectable, string providerKey)
             * {
             *     return new CompiledProviderBinding(key, requiredBy, mustBeInjectable, providerKey);
             * }
             */
            var factory = new MethodDefinition(
                "ProviderBindingFactory_" + (factoryMethodsGenerated++),
                MethodAttributes.Private,
                References.Binding);
            
            factory.Parameters.Add (new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            factory.Parameters.Add (new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
            factory.Parameters.Add (new ParameterDefinition("mustBeInjectable", ParameterAttributes.None, ModuleDefinition.TypeSystem.Boolean));
            factory.Parameters.Add (new ParameterDefinition("providerKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            
            var il = factory.Body.GetILProcessor();
            il.Emit (OpCodes.Ldarg_1);
            il.Emit (OpCodes.Ldarg_2);
            il.Emit (OpCodes.Ldarg_3);
            il.Emit (OpCodes.Ldarg_S, factory.Parameters.Last());
            il.Emit (OpCodes.Newobj, ctor);
            il.Emit (OpCodes.Ret);
            
            return factory;
        }

        private MethodDefinition EmitModuleFactory(MethodReference ctor)
        {
            /**
             * private RuntimeModule ModuleFactory_N()
             * {
             *     return new CompiledRuntimeModuleN();
             * }
             */
            var factory = new MethodDefinition(
                "ModuleFactory_" + (factoryMethodsGenerated++),
                MethodAttributes.Private,
                References.RuntimeModule);
            
            var il = factory.Body.GetILProcessor();
            il.Emit (OpCodes.Newobj, ctor);
            il.Emit (OpCodes.Ret);
            
            return factory;
        }

        private void EmitGetInjectBinding()
        {
            /**
             * public virtual Binding GetInjectBinding(string key, string className, object requiredBy)
             * {
             *     return bindings[key]();
             * }
             */
            var getInjectBinding = new MethodDefinition(
                "GetInjectBinding",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                References.Binding);

            getInjectBinding.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            getInjectBinding.Parameters.Add(new ParameterDefinition("className", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            getInjectBinding.Parameters.Add(new ParameterDefinition("mustBeInjectable", ParameterAttributes.None, ModuleDefinition.TypeSystem.Boolean));

            var vBindingFn = new VariableDefinition("bindingFn", bindingFnType);
            getInjectBinding.Body.Variables.Add(vBindingFn);
            getInjectBinding.Body.InitLocals = true;

            var endOfFn = Instruction.Create(OpCodes.Ret);
            var loadBindingFn = Instruction.Create(OpCodes.Ldloc, vBindingFn);

            var il = getInjectBinding.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, injectsField);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloca, vBindingFn);
            il.Emit(OpCodes.Callvirt, References.DictionaryOfStringToBindingFn_TryGetValue);
            il.Emit(OpCodes.Brtrue, loadBindingFn);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Br, endOfFn);
            il.Append(loadBindingFn);
            il.Emit(OpCodes.Callvirt, bindingFnInvoke);
            il.Append(endOfFn);

            plugin.Methods.Add(getInjectBinding);
        }

        private void EmitGetLazyInjectBinding()
        {
            /**
             * public virtual Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
             * {
             *     return lazyBindings[lazyKey](key, requiredBy, lazyKey);
             * }
             */
            var getLazy = new MethodDefinition(
                "GetLazyInjectBinding",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                References.Binding);

            getLazy.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            getLazy.Parameters.Add(new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
            getLazy.Parameters.Add(new ParameterDefinition("lazyKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

            var vLazyBindingFn = new VariableDefinition(lazyFnType);
            getLazy.Body.Variables.Add(vLazyBindingFn);
            getLazy.Body.InitLocals = true;

            var loadLazyFn = Instruction.Create(OpCodes.Ldloc, vLazyBindingFn);
            var endOfFn = Instruction.Create(OpCodes.Ret);

            var il = getLazy.Body.GetILProcessor();
            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Ldfld, lazyInjectsField);
            il.Emit (OpCodes.Ldarg_3);
            il.Emit (OpCodes.Ldloca, vLazyBindingFn);
            il.Emit (OpCodes.Callvirt, References.DictionaryOfStringToLazyBindingFn_TryGetValue);
            il.Emit (OpCodes.Brtrue, loadLazyFn);
            il.Emit (OpCodes.Ldnull);
            il.Emit (OpCodes.Br, endOfFn);
            il.Append(loadLazyFn);
            il.Emit (OpCodes.Ldarg_1);
            il.Emit (OpCodes.Ldarg_2);
            il.Emit (OpCodes.Ldarg_3);
            il.Emit (OpCodes.Callvirt, lazyFnInvoke);
            il.Append(endOfFn);

            plugin.Methods.Add(getLazy);
        }

        private void EmitGetIProviderInjectBinding()
        {
            /**
             * public virtual Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
             * {
             *     return providerBindings[providerKey](key, requiredBy, mustBeInjectable, providerKey);
             * }
             */
            var getProvider = new MethodDefinition(
                "GetIProviderInjectBinding",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                References.Binding);

            getProvider.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            getProvider.Parameters.Add(new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
            getProvider.Parameters.Add(new ParameterDefinition("mustBeInjectable", ParameterAttributes.None, ModuleDefinition.TypeSystem.Boolean));
            getProvider.Parameters.Add(new ParameterDefinition("providerKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

            var providerKeyArg = getProvider.Parameters.Last();

            var vProviderFn = new VariableDefinition(providerFnType);
            getProvider.Body.Variables.Add(vProviderFn);
            getProvider.Body.InitLocals = true;

            var loadProviderFn = Instruction.Create(OpCodes.Ldloc, vProviderFn);
            var endOfFn = Instruction.Create(OpCodes.Ret);

            var il = getProvider.Body.GetILProcessor();
            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Ldfld, providersField);
            il.Emit (OpCodes.Ldarg_S, providerKeyArg);
            il.Emit (OpCodes.Ldloca, vProviderFn);
            il.Emit (OpCodes.Callvirt, References.DictionaryOfStringToProviderBindingFn_TryGetValue);
            il.Emit (OpCodes.Brtrue, loadProviderFn);
            il.Emit (OpCodes.Ldnull);
            il.Emit (OpCodes.Br, endOfFn);
            il.Append(loadProviderFn);
            il.Emit (OpCodes.Ldarg_1);
            il.Emit (OpCodes.Ldarg_2);
            il.Emit (OpCodes.Ldarg_3);
            il.Emit (OpCodes.Ldarg_S, providerKeyArg);
            il.Emit (OpCodes.Callvirt, providerFnInvoke);
            il.Append(endOfFn);

            plugin.Methods.Add(getProvider);
        }

        private void EmitGetRuntimeModule()
        {
            /**
             * public virtual RuntimeModule GetRuntimeModule(Type type, object instance)
             * {
             *     return modules[type]();
             * }
             */
            var getModule = new MethodDefinition(
                "GetRuntimeModule",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                References.RuntimeModule);

            getModule.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None, References.Type));
            getModule.Parameters.Add(new ParameterDefinition("instance", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));

            var vModuleFn = new VariableDefinition(moduleFnType);
            getModule.Body.Variables.Add(vModuleFn);
            getModule.Body.InitLocals = true;

            var endOfFn = Instruction.Create(OpCodes.Ret);
            var loadModuleFn = Instruction.Create(OpCodes.Ldloc, vModuleFn);

            var il = getModule.Body.GetILProcessor();
            il.Emit (OpCodes.Ldarg_0);
            il.Emit (OpCodes.Ldfld, modulesField);
            il.Emit (OpCodes.Ldarg_1);
            il.Emit (OpCodes.Ldloca, vModuleFn);
            il.Emit (OpCodes.Callvirt, References.DictionaryOfTypeToModuleFn_TryGetValue);
            il.Emit (OpCodes.Brtrue, loadModuleFn);
            il.Emit (OpCodes.Ldnull);
            il.Emit (OpCodes.Br, endOfFn);
            il.Append (loadModuleFn);
            il.Emit (OpCodes.Callvirt, moduleFnInvoke);
            il.Append (endOfFn);

            plugin.Methods.Add (getModule);
        }
    }
}
