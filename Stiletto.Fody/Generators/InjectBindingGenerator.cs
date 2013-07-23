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
using Mono.Cecil.Rocks;
using Stiletto.Internal.Loaders.Codegen;

namespace Stiletto.Fody.Generators
{
    public class InjectBindingGenerator : Generator
    {
        private readonly TypeDefinition injectedType;
        private readonly TypeReference importedInjectedType;
        private readonly bool isModuleInjectable;

        private MethodReference generatedCtor;
        private GenericInstanceType genericInstanceType;

        public string Key { get; private set; }
        public string MembersKey { get; private set; }
        public string BaseTypeKey { get; private set; }
        public bool IsSingleton { get; private set; }
        public MethodDefinition InjectableCtor { get; private set; }
        public IList<PropertyInfo> InjectableProperties { get; private set; }
        public bool IsModuleInjectable { get { return isModuleInjectable; } }
        public IList<InjectMemberInfo> CtorParams { get; private set; }
        public TypeDefinition InjectedType { get { return injectedType; } }

        public ModuleWeaver Weaver { get; set; }

        public bool IsVisibleToLoader { get; private set; }

        public InjectBindingGenerator(ModuleDefinition moduleDefinition, References references, TypeReference injectedType, bool isModuleInjectable)
            : base(moduleDefinition, references)
        {
            this.injectedType = injectedType.IsDefinition
                                    ? (TypeDefinition) injectedType
                                    : injectedType.Resolve();

            importedInjectedType = Import(injectedType);
            genericInstanceType = injectedType as GenericInstanceType;

            this.isModuleInjectable = isModuleInjectable;

            CtorParams = new List<InjectMemberInfo>();
            InjectableProperties = new List<PropertyInfo>();
            IsVisibleToLoader = true;
        }

        public override void Validate(IErrorReporter errorReporter)
        {
            if (injectedType.HasGenericParameters)
            {
                if (genericInstanceType == null ||
                    genericInstanceType.GenericArguments.Count != injectedType.GenericParameters.Count)
                {
                    errorReporter.LogError("Open generic types may not be injected: " + injectedType.FullName);
                    return;
                }
            }

            if (!injectedType.IsVisible())
            {
                // This type is not externally visible and can't be included in a compiled loader.
                // It can still be loaded reflectively.
                IsVisibleToLoader = false;
                errorReporter.LogWarning(injectedType.FullName + ": This type is private, and will be loaded reflectively.  Consider making it internal or public.");
            }

            Key = CompilerKeys.ForType(injectedType);
            MembersKey = CompilerKeys.GetMemberKey(injectedType);
            IsSingleton = injectedType.CustomAttributes.Any(Attributes.IsSingletonAttribute);

            var injectableCtors = injectedType
                .GetConstructors()
                .Where(ctor => ctor.CustomAttributes.Any(Attributes.IsInjectAttribute))
                .ToList();

            foreach (var ctor in injectableCtors)
            {
                if (InjectableCtor != null)
                {
                    errorReporter.LogError(string.Format("{0} has more than one injectable constructor.", injectedType.FullName));
                }

                if (!ctor.IsVisible())
                {
                    errorReporter.LogError(string.Format("{0} has an injectable constructor, but it is not accessible.  Consider making it public.", injectedType.FullName));
                }

                InjectableCtor = ctor;
            }

            InjectableProperties = injectedType
                .Properties
                .Where(p => p.DeclaringType == injectedType)
                .Where(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute))
                .Select(p => new PropertyInfo(p))
                .ToList();

            foreach (var p in InjectableProperties)
            {
                if (p.Setter == null)
                {
                    errorReporter.LogError(string.Format("{0} is marked [Inject] but has no setter.", p.MemberName));
                    continue;
                }

                if (!p.Setter.IsVisible())
                {
                    const string msg = "{0}.{1} is marked [Inject], but has no visible setter.  Consider adding a public setter.";
                    errorReporter.LogError(string.Format(msg, injectedType.FullName, p.PropertyName));
                }
            }

            if (InjectableCtor == null)
            {
                if (InjectableProperties.Count == 0 && !IsModuleInjectable) {
                    errorReporter.LogError("No injectable constructors or properties found on " + injectedType.FullName);
                }

                // XXX ben: this is wrong, I think - entry points with no ctor will still fail.
                var defaultCtor = injectedType.GetConstructors().FirstOrDefault(ctor => !ctor.HasParameters && ctor.IsVisible());
                if (defaultCtor == null && !IsModuleInjectable)
                {
                    errorReporter.LogError("Type " + injectedType.FullName + " has no [Inject] constructors and no default constructor.");
                    return;
                }

                InjectableCtor = defaultCtor;
            }
            // InjectableCtor is null iff this is a value-type entry-point with no default ctor.
            // It's OK, such types never get constructed, only "provided".
            CtorParams = InjectableCtor == null
                ? new List<InjectMemberInfo>()
                : InjectableCtor.Parameters.Select(p => new InjectMemberInfo(p)).ToList();

            var baseType = injectedType.BaseType;
            var baseTypeAsmName = baseType.Maybe(type => type.Scope)
                                          .Maybe(scope => scope.Name);

            if (baseType == null
                || baseTypeAsmName == null
                || baseTypeAsmName.StartsWith("mscorlib")
                || baseTypeAsmName.StartsWith("System")
                || baseTypeAsmName.StartsWith("Microsoft")
                || baseTypeAsmName.StartsWith("Mono")) {
                // We can safely skip types known not to have [Inject] bindings, i.e. types
                // from the BCL, etc.
                BaseTypeKey = null;
            } else {
                // Otherwise, base types might have [Inject] properties that we'll need
                // to account for.
                BaseTypeKey = Weaver.EnqueueBaseTypeBinding(baseType)
                    ? CompilerKeys.ForType(baseType)
                    : null;
            }
        }

        public override TypeDefinition Generate(IErrorReporter errorReporter)
        {
            // If an entry-point is declared that has no injectables (i.e. a primitive type),
            // there's nothing to emit.
            if ((InjectableCtor == null || !InjectableCtor.CustomAttributes.Any(Attributes.IsInjectAttribute))
                && InjectableProperties.Count == 0
                && BaseTypeKey == null)
            {
                Conditions.Assert(IsModuleInjectable, "Non-injectable types must have a constructor!");
                return null;
            }

            var injectBinding = new TypeDefinition(
                injectedType.Namespace,
                injectedType.Name + CodegenLoader.InjectSuffix,
                injectedType.Attributes,
                References.Binding);

            injectBinding.CustomAttributes.Add(new CustomAttribute(References.CompilerGeneratedAttribute));

            var propertyFields = new List<FieldDefinition>(InjectableProperties.Count);

            foreach (var property in InjectableProperties) {
                var propertyBinding = new FieldDefinition(property.PropertyName, FieldAttributes.Private, References.Binding);
                injectBinding.Fields.Add(propertyBinding);
                propertyFields.Add(propertyBinding);
            }

            FieldDefinition ctorParamsField = null;
            if (CtorParams.Count > 0) {
                ctorParamsField = new FieldDefinition("ctorParamBindings", FieldAttributes.Private, References.BindingArray);
                injectBinding.Fields.Add(ctorParamsField);
            }

            FieldDefinition baseTypeField = null;
            if (BaseTypeKey != null) {
                baseTypeField = new FieldDefinition("baseTypeBinding", FieldAttributes.Private, References.Binding);
                injectBinding.Fields.Add(baseTypeField);
            }

            EmitCtor(injectBinding);
            EmitResolve(injectBinding, propertyFields, ctorParamsField, baseTypeField);
            EmitGetDependencies(injectBinding, propertyFields, ctorParamsField, baseTypeField);
            var injectProperties = EmitInjectProperties(injectBinding, baseTypeField, propertyFields);
            EmitGet(injectBinding, injectProperties, ctorParamsField);

            if (injectedType.DeclaringType != null) {
                injectBinding.DeclaringType = injectedType.DeclaringType;
            }

            return injectBinding;
        }

        public override KeyedCtor GetKeyedCtor()
        {
            if (generatedCtor == null)
            {
                return null;
            }

            return new KeyedCtor(Key, generatedCtor);
        }

        private void EmitCtor(TypeDefinition injectBinding)
        {
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                References.Void);

            var il = ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, Key);
            il.Emit(OpCodes.Ldstr, MembersKey);
            il.EmitBoolean(IsSingleton);
            il.Emit(OpCodes.Ldtoken, importedInjectedType);
            il.Emit(OpCodes.Call, References.Type_GetTypeFromHandle);
            il.Emit(OpCodes.Call, References.Binding_Ctor);

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(ctor);
            generatedCtor = ctor;
        }

        private void EmitResolve(TypeDefinition injectBinding, IList<FieldDefinition> propertyFields, FieldDefinition paramsField, FieldDefinition baseTypeField)
        {
            /**
             * public override void Resolve(Resolver resolver)
             * {
             *     // if properties
             *     propBinding0 = resolver.RequestBinding(Key(prop0), prop0.FullName, mustBeInjectable: true);
             *     ...
             *     propBindingN = resolver.RequestBinding(Key(propN), propN.FullName, mustBeInjectable: true);
             *     
             *     // if ctor params
             *     ctorParams = new Binding[params.Count];
             *     ctorParams[0..n] = resolver.RequestBinding(Key(params[0..n]), ctor.FullName, mustBeInjectable: true);
             *     
             *     // if base type
             *     baseTypeBinding = resolver.RequestBinding(Key(baseType), typeof(baseType), mustBeInjectable: false);
             * }
             */

            var resolve = new MethodDefinition(
                "Resolve",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Void);

            resolve.Parameters.Add(new ParameterDefinition(References.Resolver));

            var vParamsArray = paramsField != null
                                   ? new VariableDefinition("ctorParamsBindings", References.BindingArray)
                                   : null;

            if (vParamsArray != null) {
                resolve.Body.Variables.Add(vParamsArray);
                resolve.Body.InitLocals = true;
            }

            var il = resolve.Body.GetILProcessor();

            for (var i = 0; i < InjectableProperties.Count; ++i) {
                var property = InjectableProperties[i];
                var field = propertyFields[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, property.Key);
                il.Emit(OpCodes.Ldstr, injectedType.FullName + "." + property.MemberName);
                il.EmitBoolean(true);  // mustBeInjectable
                il.EmitBoolean(false); // isLibrary
                il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
                il.Emit(OpCodes.Stfld, field);
            }

            if (paramsField != null) {
                il.Emit(OpCodes.Ldc_I4, CtorParams.Count);
                il.Emit(OpCodes.Newarr, References.Binding);
                il.Emit(OpCodes.Stloc, vParamsArray);

                for (var i = 0; i < CtorParams.Count; ++i) {
                    var param = CtorParams[i];

                    il.Emit(OpCodes.Ldloc, vParamsArray);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldstr, param.Key);
                    il.Emit(OpCodes.Ldstr, injectedType.FullName + "::.ctor");
                    il.EmitBoolean(true);
                    il.EmitBoolean(true);
                    il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
                    il.Emit(OpCodes.Stelem_Ref);
                }

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldloc, vParamsArray);
                il.Emit(OpCodes.Stfld, paramsField);
            }

            if (baseTypeField != null) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, BaseTypeKey);
                il.Emit(OpCodes.Ldtoken, Import(injectedType.BaseType));
                il.Emit(OpCodes.Call, References.Type_GetTypeFromHandle);
                il.EmitBoolean(false);
                il.EmitBoolean(true);
                il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
                il.Emit(OpCodes.Stfld, baseTypeField);
            }

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(resolve);
        }

        private void EmitGetDependencies(TypeDefinition injectBinding, IEnumerable<FieldDefinition> propertyFields, FieldDefinition ctorParamsField, FieldDefinition baseTypeField)
        {
            var getDependencies = new MethodDefinition(
                "GetDependencies",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Void);

            getDependencies.Parameters.Add(new ParameterDefinition("injectDependencies", ParameterAttributes.None, References.SetOfBindings));
            getDependencies.Parameters.Add(new ParameterDefinition("propertyDependencies", ParameterAttributes.None, References.SetOfBindings));

            var il = getDependencies.Body.GetILProcessor();

            if (ctorParamsField != null) {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, ctorParamsField);
                il.Emit(OpCodes.Callvirt, References.SetOfBindings_UnionWith);
            }

            foreach (var field in propertyFields) {
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.SetOfBindings_Add);
                il.Emit(OpCodes.Pop);
            }

            if (baseTypeField != null) {
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, baseTypeField);
                il.Emit(OpCodes.Callvirt, References.SetOfBindings_Add);
                il.Emit(OpCodes.Pop);
            }

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(getDependencies);
        }

        private void EmitGet(TypeDefinition injectBinding, MethodReference injectProperties, FieldDefinition ctorParamsField)
        {
            var get = new MethodDefinition(
                "Get",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Object);

            VariableDefinition vResult = null;
            if (injectProperties != null) {
                vResult = new VariableDefinition("result", References.Object);
                get.Body.Variables.Add(vResult);
                get.Body.InitLocals = true;
            }

            var il = get.Body.GetILProcessor();
            for (var i = 0; i < CtorParams.Count; ++i) {
                var param = CtorParams[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, ctorParamsField);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Callvirt, References.Binding_Get);
                il.Cast(Import(param.Type));
            }

            il.Emit(OpCodes.Newobj, Import(InjectableCtor));

            if (vResult != null) {
                il.Emit(OpCodes.Stloc, vResult);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldloc, vResult);
                il.Emit(OpCodes.Callvirt, injectProperties);
                il.Emit(OpCodes.Ldloc, vResult);
            }

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(get);
        }

        private MethodReference EmitInjectProperties(TypeDefinition injectBinding, FieldDefinition baseTypeField, IList<FieldDefinition> propertyFields)
        {
            if (propertyFields.Count == 0 && baseTypeField == null) {
                return null;
            }

            var injectProperties = new MethodDefinition(
                "InjectProperties",
                MethodAttributes.Public | MethodAttributes.Virtual,
                References.Void);

            injectProperties.Parameters.Add(new ParameterDefinition(References.Object));

            var vObj = new VariableDefinition("inject", importedInjectedType);
            injectProperties.Body.Variables.Add(vObj);
            injectProperties.Body.InitLocals = true;

            var il = injectProperties.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, importedInjectedType);
            il.Emit(OpCodes.Stloc, vObj);

            for (var i = 0; i < InjectableProperties.Count; ++i) {
                var property = InjectableProperties[i];
                var field = propertyFields[i];

                il.Emit(OpCodes.Ldloc, vObj);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.Binding_Get);
                il.Cast(property.Type);
                
                il.Emit(OpCodes.Callvirt, Import(property.Setter));
            }

            if (baseTypeField != null) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, baseTypeField);
                il.Emit(OpCodes.Ldloc, vObj);
                il.Emit(OpCodes.Callvirt, References.Binding_InjectProperties);
            }

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(injectProperties);
            return injectProperties;
        }
    }
}
