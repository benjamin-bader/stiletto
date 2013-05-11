using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Abra.Fody.Generators
{
    public class InjectBindingGenerator : Generator
    {
        private readonly TypeDefinition injectedType;
        private readonly bool isEntryPoint;

        public string Key { get; private set; }
        public string MembersKey { get; private set; }
        public string BaseTypeKey { get; private set; }
        public bool IsSingleton { get; private set; }
        public MethodDefinition InjectableCtor { get; private set; }
        public IList<PropertyDefinition> InjectableProperties { get; private set; }
        public bool IsEntryPoint { get { return isEntryPoint; } }
        public IList<ParameterDefinition> CtorParams { get; private set; }
        public TypeDefinition InjectedType { get { return injectedType; } }

        public InjectBindingGenerator(ModuleDefinition moduleDefinition, TypeReference injectedType, bool isEntryPoint)
            : base(moduleDefinition)
        {
            this.injectedType = injectedType.IsDefinition
                                    ? (TypeDefinition) injectedType
                                    : ModuleDefinition.Import(injectedType).Resolve();
            this.isEntryPoint = isEntryPoint;
        }

        public override void Validate(IWeaver weaver)
        {
            if (injectedType.HasGenericParameters) {
                weaver.LogError("Open generic types may not be injected: " + injectedType.FullName);
                return;
            }

            Key = CompilerKeys.ForType(injectedType);
            MembersKey = CompilerKeys.GetMemberKey(injectedType);
            IsSingleton = injectedType.CustomAttributes.Any(Attributes.IsSingletonAttribute);

            var injectableCtors = injectedType
                .GetConstructors()
                .Where(ctor => ctor.CustomAttributes.Any(Attributes.IsInjectAttribute))
                .ToList();

            foreach (var ctor in injectableCtors) {
                if (InjectableCtor != null) {
                    weaver.LogError(string.Format("{0} has more than one injectable constructor.", injectedType.FullName));
                }

                if (!ctor.Attributes.IsVisible()) {
                    weaver.LogError("{0} has an injectable constructor, but it is not accessible.  Consider making it public.");
                }

                InjectableCtor = ctor;
            }

            InjectableProperties = injectedType
                .Properties
                .Where(p => p.DeclaringType == injectedType)
                .Where(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute))
                .ToList();

            foreach (var p in InjectableProperties) {
                if (p.SetMethod == null) {
                    weaver.LogError(string.Format("{0} is marked [Inject] but has no setter.", p.FullName));
                    continue;
                }

                if (!p.SetMethod.Attributes.IsVisible()) {
                    const string msg = "{0}.{1} is marked [Inject], but has no visible setter.  Consider adding a public setter.";
                    weaver.LogError(string.Format(msg, injectedType.FullName, p.Name));
                    continue;
                }

                var key = CompilerKeys.ForProperty(p);
                var providerKey = CompilerKeys.GetProviderKey(key);
                if (providerKey != null) {
                    var genericPropertyType = (GenericInstanceType) p.PropertyType;
                    weaver.EnqueueProviderBinding(genericPropertyType.GenericArguments.Single());
                }

                var lazyKey = CompilerKeys.GetLazyKey(key);
                if (lazyKey != null) {
                    var genericPropertyType = (GenericInstanceType) p.PropertyType;
                    weaver.EnqueueLazyBinding(genericPropertyType.GenericArguments.Single());
                }
            }

            if (InjectableCtor == null) {
                if (InjectableProperties.Count == 0 && !IsEntryPoint) {
                    weaver.LogError("No injectable constructors or properties found on " + injectedType.FullName);
                }

                var defaultCtor = injectedType.GetConstructors().FirstOrDefault(ctor => !ctor.HasParameters);
                if (defaultCtor == null) {
                    weaver.LogError("Type " + injectedType.FullName + " has no [Inject] constructors and no default constructor.");
                    return;
                }

                InjectableCtor = defaultCtor;
            }

            CtorParams = InjectableCtor.Parameters.ToList();

            foreach (var param in CtorParams) {
                var key = CompilerKeys.ForParam(param);
                var providerKey = CompilerKeys.GetProviderKey(key);
                if (providerKey != null) {
                    var genericParamType = (GenericInstanceType) param.ParameterType;
                    weaver.EnqueueProviderBinding(genericParamType.GenericArguments.Single());
                }

                var lazyKey = CompilerKeys.GetLazyKey(key);
                if (lazyKey != null) {
                    var genericParamType = (GenericInstanceType) param.ParameterType;
                    weaver.EnqueueLazyBinding(genericParamType.GenericArguments.Single());
                }
            }

            var baseType = injectedType.BaseType;
            var baseTypeScope = baseType == null ? null : baseType.Scope;
            var baseTypeAsmName = baseTypeScope == null ? null : baseTypeScope.Name;

            if (baseType == null
                || baseTypeAsmName == null
                || baseTypeAsmName.StartsWith("mscorlib")
                || baseTypeAsmName.StartsWith("System")
                || baseTypeAsmName.StartsWith("Microsoft")
                || baseTypeAsmName.StartsWith("Mono")) {
                BaseTypeKey = null;
            } else {
                BaseTypeKey = CompilerKeys.ForType(baseType);
            }
        }

        public override TypeDefinition Generate(IWeaver weaver)
        {
            var injectBinding = new TypeDefinition(
                injectedType.Namespace,
                injectedType.Name + Internal.Plugins.Codegen.CodegenPlugin.InjectSuffix,
                injectedType.Attributes,
                References.Binding);

            var propertyFields = new List<FieldDefinition>(InjectableProperties.Count);

            foreach (var property in InjectableProperties) {
                var propertyBinding = new FieldDefinition(property.Name, FieldAttributes.Private, References.Binding);
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

        private void EmitCtor(TypeDefinition injectBinding)
        {
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                ModuleDefinition.TypeSystem.Void);

            var il = ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, Key);
            il.Emit(OpCodes.Ldstr, MembersKey);
            il.EmitBoolean(IsSingleton);
            il.EmitType(ModuleDefinition.Import(injectedType));
            il.Emit(OpCodes.Call, References.Binding_Ctor);

            il.Emit(OpCodes.Ret);

            injectBinding.Methods.Add(ctor);
        }

        private void EmitResolve(TypeDefinition injectBinding, IList<FieldDefinition> propertyFields, FieldDefinition paramsField, FieldDefinition baseTypeField)
        {
            var resolve = new MethodDefinition(
                "Resolve",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Void);

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
                il.Emit(OpCodes.Ldstr, CompilerKeys.ForProperty(property));
                il.Emit(OpCodes.Ldstr, property.FullName);
                il.EmitBoolean(true);
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
                    il.Emit(OpCodes.Ldstr, CompilerKeys.ForParam(param));
                    il.Emit(OpCodes.Ldstr, InjectableCtor.FullName);
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
                il.EmitType(injectedType.BaseType);
                il.EmitBoolean(false);
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
                ModuleDefinition.TypeSystem.Void);

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
                ModuleDefinition.TypeSystem.Object);

            VariableDefinition vResult = null;
            if (injectProperties != null) {
                vResult = new VariableDefinition("result", ModuleDefinition.TypeSystem.Object);
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
                il.Cast(param.ParameterType);
            }

            il.Emit(OpCodes.Newobj, ModuleDefinition.Import(InjectableCtor));

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
                ModuleDefinition.TypeSystem.Void);

            injectProperties.Parameters.Add(new ParameterDefinition(ModuleDefinition.TypeSystem.Object));

            var vObj = new VariableDefinition("inject", injectedType);
            injectProperties.Body.Variables.Add(vObj);
            injectProperties.Body.InitLocals = true;

            var il = injectProperties.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, injectedType);
            il.Emit(OpCodes.Stloc, vObj);

            for (var i = 0; i < InjectableProperties.Count; ++i) {
                var property = InjectableProperties[i];
                var field = propertyFields[i];
                var setter = property.SetMethod;

                il.Emit(OpCodes.Ldloc, vObj);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.Binding_Get);
                il.Cast(property.PropertyType);
                
                il.Emit(OpCodes.Callvirt, ModuleDefinition.Import(setter));
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
