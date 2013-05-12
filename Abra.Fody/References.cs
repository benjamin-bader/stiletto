using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using System.Runtime.CompilerServices;

namespace Abra.Fody
{
	/// <summary>
	/// Exposes references to external types and methods scoped to the module
	/// being woven.
	/// </summary>
    public static class References
    {
        public static TypeReference Binding { get; private set; }
        public static MethodReference Binding_Ctor { get; private set; }
        public static MethodReference Binding_Resolve { get; private set; }
        public static MethodReference Binding_GetDependencies { get; private set; }
        public static MethodReference Binding_Get { get; private set; }
        public static MethodReference Binding_InjectProperties { get; private set; }
        public static MethodReference Binding_RequiredByGetter { get; private set; }

        public static TypeReference BindingArray { get; private set; }

        public static TypeReference RuntimeModule { get; private set; }
        public static MethodReference RuntimeModule_Ctor { get; private set; }
        public static MethodReference RuntimeModule_Module { get; private set; }

        public static TypeReference Container { get; private set; }
        public static MethodReference Container_Create { get; private set; }
        public static MethodReference Container_CreateWithPlugin { get; private set; }

        public static TypeReference IPlugin { get; private set; }
		public static MethodReference IPlugin_GetInjectBinding { get; private set; }
		public static MethodReference IPlugin_GetLazyInjectBinding { get; private set; }
		public static MethodReference IPlugin_GetIProviderInjectBinding { get; private set; }
		public static MethodReference IPlugin_GetRuntimeModue { get; private set; }

        public static TypeReference SetOfBindings { get; private set; }
        public static MethodReference SetOfBindings_Add { get; private set; }
        public static MethodReference SetOfBindings_UnionWith { get; private set; }

        public static TypeReference DictionaryOfStringToBinding { get; private set; }
        public static MethodReference DictionaryOfStringToBinding_Add { get; private set; }

		public static TypeReference DictionaryOfStringToBindingFn { get; private set; }
		public static MethodReference DictionaryOfStringToBindingFn_New { get; private set; }
		public static MethodReference DictionaryOfStringToBindingFn_Add { get; private set; }
		public static MethodReference DictionaryOfStringToBindingFn_Get { get; private set; }

		public static TypeReference DictionaryOfStringToLazyBindingFn { get; private set; }
		public static MethodReference DictionaryOfStringToLazyBindingFn_New { get; private set; }
		public static MethodReference DictionaryOfStringToLazyBindingFn_Add { get; private set; }
		public static MethodReference DictionaryOfStringToLazyBindingFn_Get { get; private set; }

		public static TypeReference DictionaryOfStringToProviderBindingFn { get; private set; }
		public static MethodReference DictionaryOfStringToProviderBindingFn_New { get; private set; }
		public static MethodReference DictionaryOfStringToProviderBindingFn_Add { get; private set; }
		public static MethodReference DictionaryOfStringToProviderBindingFn_Get { get; private set; }

		public static TypeReference DictionaryOfTypeToModuleFn { get; private set; }
		public static MethodReference DictionaryOfTypeToModuleFn_New { get; private set; }
		public static MethodReference DictionaryOfTypeToModuleFn_Add { get; private set; }
		public static MethodReference DictionaryOfTypeToModuleFn_Get { get; private set; }

        public static TypeReference Resolver { get; private set; }
        public static MethodReference Resolver_RequestBinding { get; private set; }

        public static TypeReference Type { get; private set; }
        public static MethodReference Type_GetTypeFromHandle { get; private set; }

        public static TypeReference InjectAttribute { get; private set; }
        public static TypeReference ModuleAttribute { get; private set; }
        public static TypeReference NamedAttribute { get; private set; }
        public static TypeReference ProvidesAttribute { get; private set; }
        public static TypeReference SingletonAttribute { get; private set; }

        public static TypeReference LazyOfT { get; private set; }

        public static TypeReference FuncOfT { get; private set; }
		public static TypeReference FuncOfT4 { get; private set; }
		public static TypeReference FuncOfT5 { get; private set; }

        public static TypeReference IProviderOfT { get; private set; }
        public static MethodReference IProviderOfT_Get { get; private set; }

		public static MethodReference CompilerGeneratedAttribute { get; private set; }

        public static void Initialize(ModuleDefinition module)
        {
            Binding = module.Import(typeof (Internal.Binding));
            Binding_Ctor = module.Import(typeof(Internal.Binding).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(bool), typeof(object) }, null));
            Binding_Resolve = module.Import(typeof (Internal.Binding).GetMethod("Resolve"));
            Binding_GetDependencies = module.Import(typeof (Internal.Binding).GetMethod("GetDependencies"));
            Binding_Get = module.Import(typeof (Internal.Binding).GetMethod("Get"));
            Binding_InjectProperties = module.Import(typeof (Internal.Binding).GetMethod("InjectProperties"));
            Binding_RequiredByGetter = module.Import(typeof (Internal.Binding).GetProperty("RequiredBy").GetGetMethod());

            BindingArray = module.Import(typeof (Internal.Binding[]));

            RuntimeModule = module.Import(typeof (Internal.RuntimeModule));
            RuntimeModule_Ctor = module.Import(typeof(Internal.RuntimeModule)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                                null,
                                new[] { typeof(Type), typeof(string[]), typeof(Type[]), typeof(bool) },
                                null));
            RuntimeModule_Module = module.Import(typeof (Internal.RuntimeModule).GetProperty("Module").GetGetMethod());

            Container = module.Import(typeof (Container));
            Container_Create = module.Import(typeof (Container).GetMethod("Create", new[] {typeof (object[])}));
            Container_CreateWithPlugin = module.Import(typeof (Container).GetMethod("CreateWithPlugin"));

            IPlugin = module.Import(typeof (Internal.IPlugin));
			IPlugin_GetInjectBinding = module.Import (typeof (Internal.IPlugin).GetMethod("GetInjectBinding"));
			IPlugin_GetLazyInjectBinding = module.Import(typeof (Internal.IPlugin).GetMethod("GetLazyInjectBinding"));
			IPlugin_GetIProviderInjectBinding = module.Import(typeof (Internal.IPlugin).GetMethod("GetIProviderInjectBinding"));
			IPlugin_GetRuntimeModue = module.Import(typeof(Internal.IPlugin).GetMethod("GetRuntimeModule"));

            SetOfBindings = module.Import(typeof (ISet<Internal.Binding>));
            SetOfBindings_Add = module.Import(typeof (ISet<Internal.Binding>).GetMethod("Add"));
            SetOfBindings_UnionWith = module.Import(typeof (ISet<Internal.Binding>).GetMethod("UnionWith"));

            DictionaryOfStringToBinding = module.Import(typeof (IDictionary<string, Internal.Binding>));
            DictionaryOfStringToBinding_Add = module.Import(typeof(IDictionary<string, Internal.Binding>).GetMethod("Add"));

			var tDictOfStringToBindingFn = typeof(Dictionary<string, Func<Internal.Binding>>);
			DictionaryOfStringToBindingFn = module.Import (tDictOfStringToBindingFn);
			DictionaryOfStringToBindingFn_New = module.Import (tDictOfStringToBindingFn.GetConstructor(new[] { typeof(StringComparer) }));
			DictionaryOfStringToBindingFn_Add = module.Import (tDictOfStringToBindingFn.GetMethod("Add"));
			DictionaryOfStringToBindingFn_Get = module.Import (tDictOfStringToBindingFn.GetProperty("Item").GetGetMethod());

			var tLazyBindingDict = typeof(Dictionary<string, Func<string, object, string, Internal.Binding>>);
			DictionaryOfStringToLazyBindingFn = module.Import (tLazyBindingDict);
			DictionaryOfStringToLazyBindingFn_New = module.Import(tLazyBindingDict.GetConstructor(new[] { typeof(StringComparer) }));
			DictionaryOfStringToLazyBindingFn_Add = module.Import(tLazyBindingDict.GetMethod("Add"));
			DictionaryOfStringToLazyBindingFn_Get = module.Import(tLazyBindingDict.GetProperty("Item").GetGetMethod());

			var tProviderDict = typeof(Dictionary<string, Func<string, object, bool, string, Internal.Binding>>);
			DictionaryOfStringToProviderBindingFn = module.Import (tProviderDict);
			DictionaryOfStringToProviderBindingFn_New = module.Import(tProviderDict.GetConstructor(new[] { typeof(StringComparer) }));
			DictionaryOfStringToProviderBindingFn_Add = module.Import(tProviderDict.GetMethod("Add"));
			DictionaryOfStringToProviderBindingFn_Get = module.Import(tProviderDict.GetProperty("Item").GetGetMethod());

			var tModuleDict = typeof(Dictionary<Type, Func<Internal.RuntimeModule>>);
			DictionaryOfTypeToModuleFn = module.Import (tModuleDict);
			DictionaryOfTypeToModuleFn_New = module.Import(tModuleDict.GetConstructor(new Type[0]));
			DictionaryOfTypeToModuleFn_Add = module.Import(tModuleDict.GetMethod("Add"));
			DictionaryOfTypeToModuleFn_Get = module.Import(tModuleDict.GetProperty("Item").GetGetMethod());

            Resolver = module.Import(typeof (Internal.Resolver));
            Resolver_RequestBinding = module.Import(typeof (Internal.Resolver).GetMethod("RequestBinding"));

            Type = module.Import(typeof (Type));
            Type_GetTypeFromHandle = module.Import(typeof (Type).GetMethod("GetTypeFromHandle"));

            InjectAttribute = module.Import(typeof (InjectAttribute));
            ModuleAttribute = module.Import(typeof (ModuleAttribute));
            NamedAttribute = module.Import(typeof (NamedAttribute));
            ProvidesAttribute = module.Import(typeof (ProvidesAttribute));
            SingletonAttribute = module.Import(typeof (SingletonAttribute));

            LazyOfT = module.Import(typeof (Lazy<>));

            FuncOfT = module.Import(typeof (Func<>));
			FuncOfT4 = module.Import(typeof (Func<,,,>));
			FuncOfT5 = module.Import(typeof (Func<,,,,>));

            IProviderOfT = module.Import(typeof (IProvider<>));
            IProviderOfT_Get = module.Import(typeof (IProvider<>).GetMethod("Get"));

			CompilerGeneratedAttribute = module.Import(typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0]));
        }
    }
}
