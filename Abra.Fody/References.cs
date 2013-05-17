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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Abra.Internal.Plugins.Codegen;
using Mono.Cecil;
using System.Runtime.CompilerServices;

namespace Abra.Fody
{
    /// <summary>
    /// Exposes references to external types and methods scoped to the module
    /// being woven.
    /// </summary>
    public class References
    {
        public TypeReference Binding { get; private set; }
        public MethodReference Binding_Ctor { get; private set; }
        public MethodReference Binding_Resolve { get; private set; }
        public MethodReference Binding_GetDependencies { get; private set; }
        public MethodReference Binding_Get { get; private set; }
        public MethodReference Binding_InjectProperties { get; private set; }
        public MethodReference Binding_RequiredByGetter { get; private set; }
        public MethodReference Binding_IsLibrarySetter { get; private set; }

        public TypeReference ProviderMethodBindingBase { get; private set; }
        public MethodReference ProviderMethodBindingBase_Ctor { get; private set; }

        public TypeReference BindingArray { get; private set; }

        public TypeReference RuntimeModule { get; private set; }
        public MethodReference RuntimeModule_Ctor { get; private set; }
        public MethodReference RuntimeModule_Module { get; private set; }

        public TypeReference Container { get; private set; }
        public MethodReference Container_Create { get; private set; }
        public MethodReference Container_CreateWithPlugins { get; private set; }

        public TypeReference IPlugin { get; private set; }
        public MethodReference IPlugin_GetInjectBinding { get; private set; }
        public MethodReference IPlugin_GetLazyInjectBinding { get; private set; }
        public MethodReference IPlugin_GetIProviderInjectBinding { get; private set; }
        public MethodReference IPlugin_GetRuntimeModue { get; private set; }

        public TypeReference SetOfBindings { get; private set; }
        public MethodReference SetOfBindings_Add { get; private set; }
        public MethodReference SetOfBindings_UnionWith { get; private set; }

        public TypeReference DictionaryOfStringToBinding { get; private set; }
        public MethodReference DictionaryOfStringToBinding_Add { get; private set; }

        public TypeReference DictionaryOfStringToBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_Get { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfStringToLazyBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_Get { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfStringToProviderBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_Get { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfTypeToModuleFn { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_New { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_Add { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_Get { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_TryGetValue { get; private set; }

        public TypeReference Resolver { get; private set; }
        public MethodReference Resolver_RequestBinding { get; private set; }

        public TypeReference Type { get; private set; }
        public MethodReference Type_GetTypeFromHandle { get; private set; }

        public TypeReference InjectAttribute { get; private set; }
        public TypeReference ModuleAttribute { get; private set; }
        public TypeReference NamedAttribute { get; private set; }
        public TypeReference ProvidesAttribute { get; private set; }
        public TypeReference SingletonAttribute { get; private set; }

        public TypeReference LazyOfT { get; private set; }

        public TypeReference FuncOfT { get; private set; }
        public TypeReference FuncOfT4 { get; private set; }
        public TypeReference FuncOfT5 { get; private set; }

        public TypeReference IProviderOfT { get; private set; }
        public MethodReference IProviderOfT_Get { get; private set; }

        public MethodReference CompilerGeneratedAttribute { get; private set; }

        public TypeReference ProcessedAssemblyAttribute { get; private set; }
        public MethodReference ProcessedAssemblyAttribute_Ctor { get; private set; }

        public References(ModuleDefinition module)
        {
            Binding = module.Import(typeof (Internal.Binding));
            Binding_Ctor = module.Import(typeof(Internal.Binding).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(bool), typeof(object) }, null));
            Binding_Resolve = module.Import(typeof (Internal.Binding).GetMethod("Resolve"));
            Binding_GetDependencies = module.Import(typeof (Internal.Binding).GetMethod("GetDependencies"));
            Binding_Get = module.Import(typeof (Internal.Binding).GetMethod("Get"));
            Binding_InjectProperties = module.Import(typeof (Internal.Binding).GetMethod("InjectProperties"));
            Binding_RequiredByGetter = module.Import(typeof (Internal.Binding).GetProperty("RequiredBy").GetGetMethod());
            Binding_IsLibrarySetter = module.Import(typeof (Internal.Binding).GetProperty("IsLibrary").GetSetMethod());

            ProviderMethodBindingBase = module.Import(typeof (Internal.ProviderMethodBindingBase));
            ProviderMethodBindingBase_Ctor = module.Import(typeof(Internal.ProviderMethodBindingBase).GetConstructor(new[] { typeof(string), typeof(string), typeof(bool), typeof(object), typeof(string), typeof(string) }));

            BindingArray = module.Import(typeof (Internal.Binding[]));

            RuntimeModule = module.Import(typeof (Internal.RuntimeModule));
            RuntimeModule_Ctor = module.Import(typeof(Internal.RuntimeModule)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                                null,
                                new[] { typeof(Type), typeof(string[]), typeof(Type[]), typeof(bool), typeof(bool) },
                                null));
            RuntimeModule_Module = module.Import(typeof (Internal.RuntimeModule).GetProperty("Module").GetGetMethod());

            Container = module.Import(typeof (Container));
            Container_Create = module.Import(typeof (Container).GetMethod("Create", new[] {typeof (object[])}));
            Container_CreateWithPlugins = module.Import(typeof (Container).GetMethod("CreateWithPlugins"));

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
            DictionaryOfStringToBindingFn_TryGetValue = module.Import(tDictOfStringToBindingFn.GetMethod("TryGetValue"));

            var tLazyBindingDict = typeof(Dictionary<string, Func<string, object, string, Internal.Binding>>);
            DictionaryOfStringToLazyBindingFn = module.Import (tLazyBindingDict);
            DictionaryOfStringToLazyBindingFn_New = module.Import(tLazyBindingDict.GetConstructor(new[] { typeof(StringComparer) }));
            DictionaryOfStringToLazyBindingFn_Add = module.Import(tLazyBindingDict.GetMethod("Add"));
            DictionaryOfStringToLazyBindingFn_Get = module.Import(tLazyBindingDict.GetProperty("Item").GetGetMethod());
            DictionaryOfStringToLazyBindingFn_TryGetValue = module.Import(tLazyBindingDict.GetMethod("TryGetValue"));

            var tProviderDict = typeof(Dictionary<string, Func<string, object, bool, string, Internal.Binding>>);
            DictionaryOfStringToProviderBindingFn = module.Import (tProviderDict);
            DictionaryOfStringToProviderBindingFn_New = module.Import(tProviderDict.GetConstructor(new[] { typeof(StringComparer) }));
            DictionaryOfStringToProviderBindingFn_Add = module.Import(tProviderDict.GetMethod("Add"));
            DictionaryOfStringToProviderBindingFn_Get = module.Import(tProviderDict.GetProperty("Item").GetGetMethod());
            DictionaryOfStringToProviderBindingFn_TryGetValue = module.Import(tProviderDict.GetMethod("TryGetValue"));

            var tModuleDict = typeof(Dictionary<Type, Func<Internal.RuntimeModule>>);
            DictionaryOfTypeToModuleFn = module.Import (tModuleDict);
            DictionaryOfTypeToModuleFn_New = module.Import(tModuleDict.GetConstructor(new Type[0]));
            DictionaryOfTypeToModuleFn_Add = module.Import(tModuleDict.GetMethod("Add"));
            DictionaryOfTypeToModuleFn_Get = module.Import(tModuleDict.GetProperty("Item").GetGetMethod());
            DictionaryOfTypeToModuleFn_TryGetValue = module.Import(tModuleDict.GetMethod("TryGetValue"));

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

            ProcessedAssemblyAttribute = module.Import(typeof (ProcessedAssemblyAttribute));
            ProcessedAssemblyAttribute_Ctor = module.Import(typeof (ProcessedAssemblyAttribute).GetConstructor(new Type[0]));
        }
    }
}
