using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;

namespace Abra.Fody.Generators
{
	public class PluginGenerator : Generator
	{
		private TypeDefinition plugin;
		private FieldDefinition injectsField;
		private FieldDefinition lazyInjectsField;
		private FieldDefinition providersField;
		private FieldDefinition modulesField;

		private IEnumerable<KeyedCtor> injectBindingCtors;
		private IEnumerable<KeyedCtor> lazyBindingCtors;
		private IEnumerable<KeyedCtor> providerBindingCtors;
		private IEnumerable<Tuple<TypeReference, MethodReference>> runtimeModuleCtors;

		private MethodReference bindingFnCtor;
		private MethodReference bindingFnInvoke;

		private MethodReference lazyFnCtor;
		private MethodReference lazyFnInvoke;

		private MethodReference providerFnCtor;
		private MethodReference providerFnInvoke;

		private MethodReference moduleFnCtor;
		private MethodReference moduleFnInvoke;

		public MethodReference GeneratedCtor { get; private set; }

		private int factoryMethodsGenerated;

		public PluginGenerator(
			ModuleDefinition moduleDefinition,
			IEnumerable<KeyedCtor> injectBindingCtors,
			IEnumerable<KeyedCtor> lazyBindingCtors,
			IEnumerable<KeyedCtor> providerBindingCtors,
			IEnumerable<Tuple<TypeReference, MethodReference>> runtimeModuleCtors)
			: base(moduleDefinition)
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

			bindingFnCtor = bindingFns.Item1;
			bindingFnInvoke = bindingFns.Item2;

			lazyFnCtor = lazyFns.Item1;
			lazyFnInvoke = lazyFns.Item2;

			providerFnCtor = providerFns.Item1;
			providerFnInvoke = providerFns.Item2;

			moduleFnCtor = moduleFns.Item1;
			moduleFnInvoke = moduleFns.Item2;

			this.injectBindingCtors = Conditions.CheckNotNull(injectBindingCtors, "injectBindingCtors");
			this.lazyBindingCtors = Conditions.CheckNotNull(lazyBindingCtors, "lazyBindingCtors");
			this.providerBindingCtors = Conditions.CheckNotNull(providerBindingCtors, "providerBindingCtors");
			this.runtimeModuleCtors = Conditions.CheckNotNull(runtimeModuleCtors, "runtimeModuleCtors");
		}

		private Tuple<MethodReference, MethodReference> GetFnMethods(
			TypeReference tFn,
			params TypeReference[] generics)
		{
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

			return Tuple.Create(ctor, invoke);
		}

		public override void Validate (IWeaver weaver)
		{
		}

		public override TypeDefinition Generate (IWeaver weaver)
		{
			plugin = new TypeDefinition(
				ModuleDefinition.Assembly.Name.Name,
				"$CompiledPlugin$",
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
			// Not used here, this is the KeyedCtor consumer.
			return null;
		}

		private void EmitCtor()
		{
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
				il.EmitType(tuple.Item1);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Ldftn, factory);
				il.Emit (OpCodes.Newobj, moduleFnCtor);
				il.Emit (OpCodes.Callvirt, References.DictionaryOfTypeToModuleFn_Add);
			}

			plugin.Methods.Add(ctor);
			GeneratedCtor = ctor;
		}

		private static void AddFactoryToDict(
			ILProcessor il,
			string key,
			MethodReference factory,
			FieldDefinition field,
			MethodReference fnCtor,
			MethodReference addFn)
		{
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldfld, field);
			il.Emit (OpCodes.Ldstr, key);
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldftn, factory);
			il.Emit (OpCodes.Newobj, fnCtor);
			il.Emit (OpCodes.Callvirt, addFn);
		}

		private MethodDefinition EmitInjectFactory(MethodReference ctor)
		{
			var factory = new MethodDefinition(
				"InjectBindingFactory_" + (factoryMethodsGenerated++),
				MethodAttributes.Private,
				References.Binding);

			var il = factory.Body.GetILProcessor();
			il.Emit (OpCodes.Newobj, ctor);
			il.Emit (OpCodes.Castclass, References.Binding);
			il.Emit (OpCodes.Ret);

			return factory;
		}

		private MethodDefinition EmitLazyFactory(MethodReference ctor)
		{
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
			var getInjectBinding = new MethodDefinition(
				"GetInjectBinding",
				MethodAttributes.Public,
				References.Binding);

			getInjectBinding.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
			getInjectBinding.Parameters.Add(new ParameterDefinition("className", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
			getInjectBinding.Parameters.Add(new ParameterDefinition("mustBeInjectable", ParameterAttributes.None, ModuleDefinition.TypeSystem.Boolean));

			var il = getInjectBinding.Body.GetILProcessor();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, injectsField);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Callvirt, References.DictionaryOfStringToBindingFn_Get);
			il.Emit(OpCodes.Callvirt, bindingFnInvoke);
			il.Emit(OpCodes.Ret);

			plugin.Methods.Add(getInjectBinding);
		}

		private void EmitGetLazyInjectBinding()
		{
			var getLazy = new MethodDefinition(
				"GetLazyInjectBinding",
				MethodAttributes.Public,
				References.Binding);

			getLazy.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
			getLazy.Parameters.Add(new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
			getLazy.Parameters.Add(new ParameterDefinition("lazyKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

			var il = getLazy.Body.GetILProcessor();
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldfld, lazyInjectsField);
			il.Emit (OpCodes.Ldarg_3);
			il.Emit (OpCodes.Callvirt, References.DictionaryOfStringToLazyBindingFn_Get);
			il.Emit (OpCodes.Ldarg_1);
			il.Emit (OpCodes.Ldarg_2);
			il.Emit (OpCodes.Ldarg_3);
			il.Emit (OpCodes.Callvirt, lazyFnInvoke);
			il.Emit (OpCodes.Ret);

			plugin.Methods.Add(getLazy);
		}

		private void EmitGetIProviderInjectBinding()
		{
			var getProvider = new MethodDefinition(
				"GetIProviderInjectBinding",
				MethodAttributes.Public,
				References.Binding);

			getProvider.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));
			getProvider.Parameters.Add(new ParameterDefinition("requiredBy", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));
			getProvider.Parameters.Add(new ParameterDefinition("mustBeInjectable", ParameterAttributes.None, ModuleDefinition.TypeSystem.Boolean));
			getProvider.Parameters.Add(new ParameterDefinition("providerKey", ParameterAttributes.None, ModuleDefinition.TypeSystem.String));

			var providerKeyArg = getProvider.Parameters.Last();

			var il = getProvider.Body.GetILProcessor();
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldfld, providersField);
			il.Emit (OpCodes.Ldarg_S, providerKeyArg);
			il.Emit (OpCodes.Callvirt, References.DictionaryOfStringToProviderBindingFn_Get);
			il.Emit (OpCodes.Ldarg_1);
			il.Emit (OpCodes.Ldarg_2);
			il.Emit (OpCodes.Ldarg_3);
			il.Emit (OpCodes.Ldarg_S, providerKeyArg);
			il.Emit (OpCodes.Callvirt, providerFnInvoke);
			il.Emit (OpCodes.Ret);

			plugin.Methods.Add(getProvider);
		}

		private void EmitGetRuntimeModule()
		{
			var getModule = new MethodDefinition(
				"GetRuntimeModule",
				MethodAttributes.Public,
				References.RuntimeModule);

			getModule.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None, References.Type));
			getModule.Parameters.Add(new ParameterDefinition("instance", ParameterAttributes.None, ModuleDefinition.TypeSystem.Object));

			var il = getModule.Body.GetILProcessor();
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldfld, modulesField);
			il.Emit (OpCodes.Ldarg_1);
			il.Emit (OpCodes.Callvirt, References.DictionaryOfTypeToModuleFn_Get);
			il.Emit (OpCodes.Callvirt, moduleFnInvoke);
			il.Emit (OpCodes.Ret);

			plugin.Methods.Add (getModule);
		}
	}
}

