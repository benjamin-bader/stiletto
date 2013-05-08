using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Abra.Fody.Generators.Module
{
    public class ProviderMethodBindingGenerator : Generator
    {
        private readonly MethodDefinition providerMethod;
        private readonly TypeDefinition moduleType;
        private readonly string key;

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

        public MethodDefinition GeneratedCtor { get; private set; }

        // Sadness, but this won't be available at construction time and needs to be provided via a property.
        public TypeDefinition RuntimeModuleType { get; set; }

        public ProviderMethodBindingGenerator(ModuleDefinition moduleDefinition, TypeDefinition moduleType, MethodDefinition providerMethod)
            : base(moduleDefinition)
        {
            this.providerMethod = Conditions.CheckNotNull(providerMethod, "providerMethod");
            this.moduleType = Conditions.CheckNotNull(moduleType, "moduleType");

            key = CompilerKeys.ForReturnType(providerMethod.MethodReturnType);
        }

        public override void Validate(IWeaver weaver)
        {
            if (ProviderMethod.HasGenericParameters) {
                weaver.LogError("Provider methods cannot be generic: " + ProviderMethod.FullName);
            }
        }

        public override void Generate(IWeaver weaver)
        {
            Conditions.CheckNotNull(RuntimeModuleType, "RuntimeModuleType");
            var providerType = new TypeDefinition(
                RuntimeModuleType.Namespace,
                "ProviderBinding_" + RuntimeModuleType.NestedTypes.Count,
                TypeAttributes.NestedPublic,
                References.Binding);

            providerType.DeclaringType = RuntimeModuleType;

            var isSingleton = ProviderMethod.MethodReturnType.CustomAttributes.Any(Attributes.IsSingletonAttribute);
            var moduleField = new FieldDefinition("module", FieldAttributes.Private, ModuleType);
            providerType.Fields.Add(moduleField);

            var parameters = new List<ParameterDefinition>();
            var fields = new List<FieldDefinition>();
            foreach (var param in ProviderMethod.Parameters) {
                var field = new FieldDefinition(param.Name, FieldAttributes.Private, References.Binding);
                providerType.Fields.Add(field);
                parameters.Add(param);
                fields.Add(field);
            }

            EmitCtor(providerType, moduleField, isSingleton);
            EmitResolve(providerType, parameters, fields);
            EmitGetDependencies(providerType, fields);
            EmitGet(providerType, moduleField, parameters, fields);

            RuntimeModuleType.NestedTypes.Add(providerType);
        }

        private void EmitCtor(TypeDefinition providerBindingType, FieldReference moduleField, bool singleton)
        {
            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                ModuleDefinition.TypeSystem.Void);
            var moduleParameter = new ParameterDefinition("module", ParameterAttributes.None, ModuleType);
            ctor.Parameters.Add(moduleParameter);

            var il = ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, Key);
            il.Emit(OpCodes.Ldnull);
            il.EmitBoolean(singleton);
            il.EmitType(ModuleType);
            il.Emit(OpCodes.Call, References.Binding_Ctor);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, moduleField);

            il.Emit(OpCodes.Ret);

            providerBindingType.Methods.Add(ctor);
            GeneratedCtor = ctor;
        }

        private void EmitResolve(TypeDefinition providerBinding, IList<ParameterDefinition> parameters, IList<FieldDefinition> fields)
        {
            if (parameters.Count == 0) {
                return;
            }

            var resolve = new MethodDefinition(
                "Resolve",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Void);
            resolve.Parameters.Add(new ParameterDefinition("resolver", ParameterAttributes.None, References.Resolver));

            var il = resolve.Body.GetILProcessor();

            for (var i = 0; i < parameters.Count; ++i) {
                var param = parameters[i];
                var field = fields[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, CompilerKeys.ForParam(param));
                il.EmitType(ModuleType);
                il.EmitBoolean(true);
                il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
                il.Emit(OpCodes.Stfld, field);
            }

            il.Emit(OpCodes.Ret);

            providerBinding.Methods.Add(resolve);
        }

        private void EmitGetDependencies(TypeDefinition providerBinding, ICollection<FieldDefinition> bindings)
        {
            if (bindings.Count == 0) {
                return;
            }

            var getDependencies = new MethodDefinition(
                "GetDependencies",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Void);

            getDependencies.Parameters.Add(new ParameterDefinition("injectDependencies", ParameterAttributes.None, References.SetOfBindings));
            getDependencies.Parameters.Add(new ParameterDefinition("propertyDependencies", ParameterAttributes.None, References.SetOfBindings));

            var il = getDependencies.Body.GetILProcessor();
            il.Body.InitLocals = true;
            il.Body.Variables.Add(new VariableDefinition("binding", References.Binding));

            foreach (var field in bindings) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Stloc_0);

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldloc_0);
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
                ModuleDefinition.TypeSystem.Object);

            var il = get.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, moduleField);

            for (var i = 0; i < parameters.Count; ++i ) {
                var field = fields[i];
                var parameter = parameters[i];

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Callvirt, References.Binding_Get);
                il.Emit(OpCodes.Castclass, parameter.ParameterType);
            }

            il.Emit(OpCodes.Callvirt, ProviderMethod);
            il.Emit(OpCodes.Ret);

            providerBinding.Methods.Add(get);
        }
    }
}
