using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace Abra.Fody.Generators
{
    public class LazyBindingGenerator : Generator
    {
        private readonly TypeReference lazyElementType;
        private readonly TypeReference lazyType;
        private readonly TypeReference funcType;
        private readonly MethodReference lazyCtor;
        private readonly MethodReference funcCtor;

        public LazyBindingGenerator(ModuleDefinition moduleDefinition, TypeReference lazyElementType)
            : base(moduleDefinition)
        {
            this.lazyElementType = Conditions.CheckNotNull(lazyElementType, "lazyElementType");
            this.lazyType = References.LazyOfT.MakeGenericInstanceType(lazyElementType);

            var genericArgument = lazyElementType;
            var funcType = References.FuncOfT.MakeGenericInstanceType(genericArgument);
            var funcCtor =
                ModuleDefinition.Import(funcType.Resolve()
                                                .Methods.First(m => m.IsConstructor && m.Parameters.Count == 2))
                                .MakeHostInstanceGeneric(genericArgument);

            var lazyType = References.LazyOfT.MakeGenericInstanceType(genericArgument);
            var lazyCtor =
                ModuleDefinition.Import(lazyType.Resolve()
                                                .GetConstructors()
                                                .First(m => m.Parameters.Count == 1
                                                         && m.Parameters[0].ParameterType.Name.StartsWith("Func")))
                                .MakeHostInstanceGeneric(genericArgument);

            this.lazyType = lazyType;
            this.lazyCtor = lazyCtor;
            this.funcType = funcType;
            this.funcCtor = funcCtor;
        }

        public override void Validate(IWeaver weaver)
        {
        }

        public override void Generate(IWeaver weaver)
        {
            var t = new TypeDefinition(
                lazyElementType.Namespace,
                lazyElementType.Name + Internal.Plugins.Codegen.CodegenPlugin.LazySuffix,
                TypeAttributes.Public | TypeAttributes.Sealed,
                References.Binding);

            var lazyKeyField = new FieldDefinition("lazyKey", FieldAttributes.Private, ModuleDefinition.TypeSystem.String);
            var delegateBindingField = new FieldDefinition("delegateBinding", FieldAttributes.Private, References.Binding);
            t.Fields.Add(lazyKeyField);
            t.Fields.Add(delegateBindingField);

            EmitCtor(t, lazyKeyField);
            EmitResolve(t, lazyKeyField, delegateBindingField);
            EmitGet(t, delegateBindingField);

            ModuleDefinition.Types.Add(t);
        }

        private void EmitCtor(TypeDefinition lazyBinding, FieldDefinition lazyKeyField)
        {
            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                ModuleDefinition.TypeSystem.Void);

            ctor.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
            ctor.Parameters.Add(new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
            ctor.Parameters.Add(new ParameterDefinition("lazyKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

            var il = ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldnull);
            il.EmitBoolean(false);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, References.Binding_Ctor);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Stfld, lazyKeyField);

            il.Emit(OpCodes.Ret);

            lazyBinding.Methods.Add(ctor);
        }

        private void EmitResolve(TypeDefinition lazyBinding, FieldDefinition lazyKeyField, FieldDefinition delegateBindingField)
        {
            var resolve = new MethodDefinition(
                "Resolve",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Void);

            resolve.Parameters.Add(new ParameterDefinition(References.Resolver));

            var il = resolve.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, lazyKeyField);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, References.Binding_RequiredByGetter);
            il.EmitBoolean(true);
            il.Emit(OpCodes.Callvirt, References.Resolver_RequestBinding);
            il.Emit(OpCodes.Stfld, delegateBindingField);
            il.Emit(OpCodes.Ret);

            lazyBinding.Methods.Add(resolve);
        }

        private void EmitGet(TypeDefinition lazyBinding, FieldDefinition delegateBindingField)
        {
            var get = new MethodDefinition(
                "Get",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Object);

            var getTypedValue = new MethodDefinition(
                "GetTypedValue",
                MethodAttributes.Private | MethodAttributes.HideBySig,
                lazyElementType);

            // First we emit a helper method to serve as the body of a Func<T>,
            // because lambdas don't exist in IL
            var il = getTypedValue.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, delegateBindingField);
            il.Emit(OpCodes.Callvirt, References.Binding_Get);
            il.Cast(lazyElementType);
            il.Emit(OpCodes.Ret);
            lazyBinding.Methods.Add(getTypedValue);

            il = get.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldftn, getTypedValue);
            il.Emit(OpCodes.Newobj, funcCtor);
            il.Emit(OpCodes.Newobj, lazyCtor);
            il.Emit(OpCodes.Ret);
            lazyBinding.Methods.Add(get);
        }
    }
}
