using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;

namespace Abra.Fody
{
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

        public static TypeDefinition IPlugin { get; private set; }

        public static TypeReference SetOfBindings { get; private set; }
        public static MethodReference SetOfBindings_Add { get; private set; }
        public static MethodReference SetOfBindings_UnionWith { get; private set; }

        public static TypeReference DictionaryOfStringToBinding { get; private set; }
        public static MethodReference DictionaryOfStringToBinding_Add { get; private set; }

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
        public static MethodReference FuncOfT_Ctor { get; private set; }

        public static TypeReference IProviderOfT { get; private set; }
        public static MethodReference IProviderOfT_Get { get; private set; }

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

            IPlugin = module.Import(typeof (Internal.IPlugin)).Resolve();

            SetOfBindings = module.Import(typeof (ISet<Internal.Binding>));
            SetOfBindings_Add = module.Import(typeof (ISet<Internal.Binding>).GetMethod("Add"));
            SetOfBindings_UnionWith = module.Import(typeof (ISet<Internal.Binding>).GetMethod("UnionWith"));

            DictionaryOfStringToBinding = module.Import(typeof (IDictionary<string, Internal.Binding>));
            DictionaryOfStringToBinding_Add = module.Import(typeof(IDictionary<string, Internal.Binding>).GetMethod("Add"));

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
            FuncOfT_Ctor = module.Import(typeof (Func<>).GetConstructor(new[] {typeof (object), typeof (IntPtr)}));

            IProviderOfT = module.Import(typeof (IProvider<>));
            IProviderOfT_Get = module.Import(typeof (IProvider<>).GetMethod("Get"));
        }
    }
}
