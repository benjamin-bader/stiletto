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

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stiletto.Fody.Generators
{
    public class ProviderMethodBindingGenerator : Generator
    {
        private readonly MethodDefinition providerMethod;
        private readonly TypeDefinition moduleType;
        private readonly string key;
        private readonly bool isLibrary;

        public MethodDefinition ProviderMethod
        {
            get { return providerMethod; }
        }

        public TypeDefinition ModuleType
        {
            get { return moduleType; }
        }

        public string Key
        {
            get { return key; }
        }

        public bool IsLibrary
        {
            get { return isLibrary; }
        }

        public bool IsSingleton { get; private set; }
        public IList<string> ParamKeys { get; private set; } 

        public MethodDefinition GeneratedCtor { get; private set; }

        // Sadness, but this won't be available at construction time and needs to be provided via a property.
        public TypeDefinition RuntimeModuleType { get; set; }

        public ProviderMethodBindingGenerator(
            ModuleDefinition moduleDefinition,
            References references,
            TypeDefinition moduleType,
            MethodDefinition providerMethod,
            bool isLibrary)
            : base(moduleDefinition, references)
        {
            this.providerMethod = Conditions.CheckNotNull(providerMethod, "providerMethod");
            this.moduleType = Conditions.CheckNotNull(moduleType, "moduleType");
            this.isLibrary = isLibrary;

            var name = ProviderMethod.GetNamedAttributeName();
            key = CompilerKeys.ForType(ProviderMethod.ReturnType, name);
        }

        public override void Validate(IErrorReporter errorReporter)
        {
            ParamKeys = new List<string>();
            foreach (var param in ProviderMethod.Parameters) {
                ParamKeys.Add(CompilerKeys.ForParam(param));
            }

            if (ProviderMethod.HasGenericParameters) {
                errorReporter.LogError("Provider methods cannot be generic: " + ProviderMethod.FullName);
            }

            if (ProviderMethod.IsStatic) {
                errorReporter.LogError("Provider methods cannot be static: " + ProviderMethod.FullName);
            }

            if (ProviderMethod.MethodReturnType.ReturnType.Name == "Lazy`1") {
                errorReporter.LogError("Provider methods cannot return System.Lazy<T> directly: " + ProviderMethod.FullName);
            }

            if (ProviderMethod.ReturnType.Name == "IProvider`1") {
                errorReporter.LogError("Provider methods cannot return IProvider<T> directly: " + ProviderMethod.FullName);
            }

            if (ProviderMethod.IsPrivate) {
                errorReporter.LogError("Provider methods cannot be private: " + ProviderMethod.FullName);
            }

            if (ProviderMethod.IsAbstract) {
                errorReporter.LogError("Provider methods cannot be abstract: " + ProviderMethod.FullName);
            }
        }

        public override TypeDefinition Generate(IErrorReporter errorReporter)
        {
            Conditions.CheckNotNull(RuntimeModuleType, "RuntimeModuleType");
            var providerType = new TypeDefinition(
                RuntimeModuleType.Namespace,
                "ProviderBinding_" + RuntimeModuleType.NestedTypes.Count,
                TypeAttributes.NestedPublic,
                References.ProviderMethodBindingBase);

            providerType.CustomAttributes.Add(new CustomAttribute(References.CompilerGeneratedAttribute));
            providerType.DeclaringType = RuntimeModuleType;

            IsSingleton = ProviderMethod.CustomAttributes.Any(Attributes.IsSingletonAttribute);
            var moduleField = new FieldDefinition("module", FieldAttributes.Private, ModuleType);
            providerType.Fields.Add(moduleField);

            var parameters = new List<ParameterDefinition>();
            var fields = new List<FieldDefinition>();
            foreach (var param in ProviderMethod.Parameters) {
                var field = new FieldDefinition(param.Name, FieldAttributes.Private, References.Binding);
                providerType.Fields.Add(field);
                parameters.Add(param);
                fields.Add(field);
                ParamKeys.Add(CompilerKeys.ForParam(param));
            }

            EmitCtor(providerType, moduleField);
            EmitResolve(providerType, parameters, fields);
            EmitGetDependencies(providerType, fields);
            EmitGet(providerType, moduleField, parameters, fields);

            RuntimeModuleType.NestedTypes.Add(providerType);

            return providerType;
        }

        public override KeyedCtor GetKeyedCtor ()
        {
            // Ignored here, not needed by the ModuleGenerator.
            return null;
        }

        private void EmitCtor(TypeDefinition providerBindingType, FieldReference moduleField)
        {
            /**
             * public ProviderBinding_N(ModuleType module)
             *     : base(Key, null, IsSingleton, typeof(ModuleType), ModuleType.FullName, ProviderMethod.FullName)
             * {
             *     this.module = module;
             *     // if IsLibrary
             *     this.IsLibrary = true;
             * }
             */

            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                References.Void);
            var moduleParameter = new ParameterDefinition("module", ParameterAttributes.None, ModuleType);
            ctor.Parameters.Add(moduleParameter);

            var il = ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, Key);
            il.Emit(OpCodes.Ldnull);
            il.EmitBoolean(IsSingleton);
            il.Emit(OpCodes.Ldtoken, ModuleType);
            il.Emit(OpCodes.Call, References.Type_GetTypeFromHandle);
            il.Emit(OpCodes.Ldstr, ModuleType.FullName);
            il.Emit(OpCodes.Ldstr, ProviderMethod.Name);
            il.Emit(OpCodes.Call, References.ProviderMethodBindingBase_Ctor);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, moduleField);

            if (IsLibrary) {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitBoolean(true);
                il.Emit(OpCodes.Callvirt, References.Binding_IsLibrarySetter);
            }

            il.Emit(OpCodes.Ret);

            providerBindingType.Methods.Add(ctor);
            GeneratedCtor = ctor;
        }

        private void EmitResolve(TypeDefinition providerBinding, IList<ParameterDefinition> parameters, IList<FieldDefinition> fields)
        {
            /**
             * public override void Resolve(Resolver resolver)
             * {
             *     this.field0 = resolver.RequestBinding(ParamKeys[0], typeof(ModuleType), true, IsLibrary);
             *     ...
             *     this.fieldN = resolver.RequestBinding(ParamKeys[N], typeof(ModuleType), true, IsLibrary);
             * }
             */

            if (parameters.Count == 0) {
                return;
            }

            var resolve = new MethodDefinition(
                "Resolve",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Void);
            resolve.Parameters.Add(new ParameterDefinition("resolver", ParameterAttributes.None, References.Resolver));

            var il = resolve.Body.GetILProcessor();

            for (var i = 0; i < parameters.Count; ++i) {
                var param = parameters[i];
                var field = fields[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, ParamKeys[i]);
                il.Emit(OpCodes.Ldtoken, ModuleType);
                il.Emit(OpCodes.Call, References.Type_GetTypeFromHandle);
                il.EmitBoolean(true);
                il.EmitBoolean(IsLibrary);
                il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
                il.Emit(OpCodes.Stfld, field);
            }

            il.Emit(OpCodes.Ret);

            providerBinding.Methods.Add(resolve);
        }

        private void EmitGetDependencies(TypeDefinition providerBinding, ICollection<FieldDefinition> bindings)
        {
            /**
             * public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
             * {
             *     Binding binding;
             *     binding = this.binding0;
             *     injectDependencies.Add(binding);
             *     ...
             *     binding = this.bindingN;
             *     injectDependencies.Add(binding);
             * }
             */

            if (bindings.Count == 0) {
                return;
            }

            var getDependencies = new MethodDefinition(
                "GetDependencies",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Void);

            getDependencies.Parameters.Add(new ParameterDefinition("injectDependencies", ParameterAttributes.None, References.SetOfBindings));
            getDependencies.Parameters.Add(new ParameterDefinition("propertyDependencies", ParameterAttributes.None, References.SetOfBindings));

            var il = getDependencies.Body.GetILProcessor();

            foreach (var field in bindings) {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.SetOfBindings_Add);
                il.Emit(OpCodes.Pop); // ISet.Add returns a bool that we're ignoring
            }

            il.Emit(OpCodes.Ret);

            providerBinding.Methods.Add(getDependencies);
        }

        private void EmitGet(TypeDefinition providerBinding, FieldDefinition moduleField, IList<ParameterDefinition> parameters, IList<FieldDefinition> fields)
        {
            var get = new MethodDefinition(
                "Get",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Object);

            var il = get.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, moduleField);

            for (var i = 0; i < parameters.Count; ++i ) {
                var field = fields[i];
                var parameter = parameters[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.Binding_Get);
                il.Cast(parameter.ParameterType);
            }

            il.Emit(OpCodes.Callvirt, ProviderMethod);
            if (ProviderMethod.ReturnType.IsValueType) {
                il.Emit(OpCodes.Box, ProviderMethod.ReturnType);
            }
            il.Emit(OpCodes.Ret);

            providerBinding.Methods.Add(get);
        }
    }
}
