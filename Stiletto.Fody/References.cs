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

using System.Linq;
using Mono.Cecil.Rocks;
using Mono.Cecil;

namespace Stiletto.Fody
{
    /// <summary>
    /// Exposes references to external types and methods scoped to the module
    /// being woven.
    /// </summary>
    public class References
    {
        // ReSharper disable InconsistentNaming

        public TypeReference Void { get; private set; }
        public TypeReference Object { get; private set; }
        public TypeReference String { get; private set; }
        public TypeReference Boolean { get; private set; }

        public TypeReference Binding { get; private set; }
        public MethodReference Binding_Ctor { get; private set; }
        public MethodReference Binding_Resolve { get; private set; }
        public MethodReference Binding_GetDependencies { get; private set; }
        public MethodReference Binding_Get { get; private set; }
        public MethodReference Binding_InjectProperties { get; private set; }
        public MethodReference Binding_RequiredByGetter { get; private set; }
        public MethodReference Binding_IsLibrarySetter { get; private set; }

        public TypeReference SetBindings { get; private set; }
        public MethodReference SetBindings_AddOfT { get; private set; }

        public TypeReference BindingArray { get; private set; }

        public TypeReference RuntimeModule { get; private set; }
        public MethodReference RuntimeModule_Ctor { get; private set; }
        public MethodReference RuntimeModule_ModuleGetter { get; private set; }

        public TypeReference Container { get; private set; }
        public MethodReference Container_Create { get; private set; }
        public MethodReference Container_CreateWithLoaders { get; private set; }

        public TypeReference ILoader { get; private set; }
        public MethodReference ILoader_GetInjectBinding { get; private set; }
        public MethodReference ILoader_GetLazyInjectBinding { get; private set; }
        public MethodReference ILoader_GetIProviderInjectBinding { get; private set; }
        public MethodReference ILoader_GetRuntimeModue { get; private set; }

        public TypeReference SetOfBindings { get; private set; }
        public MethodReference SetOfBindings_Add { get; private set; }
        public MethodReference SetOfBindings_UnionWith { get; private set; }

        public TypeReference DictionaryOfStringToBinding { get; private set; }
        public MethodReference DictionaryOfStringToBinding_Add { get; private set; }

        public TypeReference DictionaryOfStringToBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfStringToLazyBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToLazyBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfStringToProviderBindingFn { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_New { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_Add { get; private set; }
        public MethodReference DictionaryOfStringToProviderBindingFn_TryGetValue { get; private set; }

        public TypeReference DictionaryOfTypeToModuleFn { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_New { get; private set; }
        public MethodReference DictionaryOfTypeToModuleFn_Add { get; private set; }
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
        public MethodReference InternalsVisibleToAttribute { get; private set; }

        public TypeReference ProcessedAssemblyAttribute { get; private set; }
        public MethodReference ProcessedAssemblyAttribute_Ctor { get; private set; }

        public MethodReference StringComparer_Ordinal_Getter { get; private set; }

        public References(ModuleDefinition module, StilettoReferences stilettoReferences)
        {
            ImportStilettoReferences(module, stilettoReferences);

            Void = module.TypeSystem.Void;
            Object = module.TypeSystem.Object;
            String = module.TypeSystem.String;
            Boolean = module.TypeSystem.Boolean;

            var assemblyResolver = module.AssemblyResolver;
            var mscorlib = assemblyResolver.Resolve("mscorlib");
            var mscorlibTypes = mscorlib.MainModule.Types;

            var system = assemblyResolver.Resolve("System");
            var systemTypes = system.MainModule.Types;

            var tDict = module.Import(mscorlibTypes.First(t => t.Name == "Dictionary`2"));

            FuncOfT = module.Import(mscorlibTypes.First(t => t.Name == "Func`1"));
            FuncOfT4 = module.Import(mscorlibTypes.First(t => t.Name == "Func`4"));
            FuncOfT5 = module.Import(mscorlibTypes.First(t => t.Name == "Func`5"));
            LazyOfT = module.Import(mscorlibTypes.First(t => t.Name == "Lazy`1"));

            var compilerGeneratedAttribute = mscorlibTypes.First(t => t.Name == "CompilerGeneratedAttribute");
            var compilerGeneratedAttributeCtor = compilerGeneratedAttribute.GetMethod(".ctor");
            CompilerGeneratedAttribute = module.Import(compilerGeneratedAttributeCtor);

            var internalsVisibleTo = mscorlibTypes.First(t => t.Name == "InternalsVisibleToAttribute");
            var internalsVisibleToCtor = internalsVisibleTo.GetConstructors().First(c => c.Parameters.Count == 1);
            InternalsVisibleToAttribute = module.Import(internalsVisibleToCtor);

            Type = module.Import(mscorlibTypes.First(t => t.Name == "Type"));
            Type_GetTypeFromHandle = module.Import(Type.Resolve().GetMethod("GetTypeFromHandle"));

            var tSetOfBindings = module.Import(systemTypes.First(t => t.Name == "ISet`1")).MakeGenericInstanceType(Binding);
            SetOfBindings = module.Import(tSetOfBindings);
            SetOfBindings_Add = module.Import(tSetOfBindings.Resolve().GetMethod("Add")).MakeHostInstanceGeneric(Binding);
            SetOfBindings_UnionWith = module.Import(tSetOfBindings.Resolve().GetMethod("UnionWith")).MakeHostInstanceGeneric(Binding);

            // Used in RuntimeModule.GetBindings(IDictionary<string, Binding>);
            var tDictOfStringToBinding = module
                .Import(mscorlibTypes.First(t => t.Name == "IDictionary`2"))
                .MakeGenericInstanceType(String, Binding);

            DictionaryOfStringToBinding = tDictOfStringToBinding;
            DictionaryOfStringToBinding_Add = module.Import(tDictOfStringToBinding.Resolve().GetMethod("Add"))
                .MakeHostInstanceGeneric(String, Binding);

            // Used in $CompiledLoader$::.ctor()
            ImportInjectBindingDictionary(module, tDict);

            // Used in $CompiledLoader$::.ctor()
            ImportLazyBindingDictionary(module, tDict);

            // Used in $CompiledLoader$::.ctor()
            ImportProviderBindingDictionary(module, tDict);

            // Used in $CompiledLoader$::.ctor()
            ImportRuntimeModuleDictionary(module, tDict);

            var stringComparer = mscorlibTypes.First(t => t.Name == "StringComparer");
            var ordinalProperty = stringComparer.GetProperty("Ordinal");
            var ordinalPropertyGetter = ordinalProperty.GetMethod;
            StringComparer_Ordinal_Getter = module.Import(ordinalPropertyGetter);
        }

        private void ImportStilettoReferences(ModuleDefinition module, StilettoReferences stilettoReferences)
        {
            Binding = module.Import(stilettoReferences.Binding);
            Binding_Ctor = module.Import(stilettoReferences.Binding_Ctor);
            Binding_GetDependencies = module.Import(stilettoReferences.Binding_GetDependencies);
            Binding_Resolve = module.Import(stilettoReferences.Binding_Resolve);
            Binding_Get = module.Import(stilettoReferences.Binding_Get);
            Binding_InjectProperties = module.Import(stilettoReferences.Binding_InjectProperties);
            Binding_RequiredByGetter = module.Import(stilettoReferences.Binding_RequiredBy_Getter);
            Binding_IsLibrarySetter = module.Import(stilettoReferences.Binding_IsLibrary_Setter);

            BindingArray = new ArrayType(Binding);

            SetBindings = module.Import(stilettoReferences.SetBindings);
            SetBindings_AddOfT = module.Import(stilettoReferences.SetBindings_Add);

            RuntimeModule = module.Import(stilettoReferences.RuntimeModule);
            RuntimeModule_Ctor = module.Import(stilettoReferences.RuntimeModule_Ctor);
            RuntimeModule_ModuleGetter = module.Import(stilettoReferences.RuntimeModule_Module_Getter);

            Container = module.Import(stilettoReferences.Container);
            Container_Create = module.Import(stilettoReferences.Container_Create);
            Container_CreateWithLoaders = module.Import(stilettoReferences.Container_CreateWithLoaders);

            ILoader = module.Import(stilettoReferences.ILoader);
            ILoader_GetInjectBinding = module.Import(stilettoReferences.ILoader_GetInjectBinding);
            ILoader_GetLazyInjectBinding = module.Import(stilettoReferences.ILoader_GetLazyInjectBinding);
            ILoader_GetIProviderInjectBinding = module.Import(stilettoReferences.ILoader_GetIProviderInjectBinding);
            ILoader_GetRuntimeModue = module.Import(stilettoReferences.ILoader_GetRuntimeModue);

            IProviderOfT = module.Import(stilettoReferences.IProviderOfT);
            IProviderOfT_Get = module.Import(stilettoReferences.IProviderOfT_Get);

            Resolver = module.Import(stilettoReferences.Resolver);
            Resolver_RequestBinding = module.Import(stilettoReferences.Resolver_RequestBinding);

            InjectAttribute = module.Import(stilettoReferences.InjectAttribute);
            ModuleAttribute = module.Import(stilettoReferences.ModuleAttribute);
            ProvidesAttribute = module.Import(stilettoReferences.ProvidesAttribute);
            NamedAttribute = module.Import(stilettoReferences.NamedAttribute);
            SingletonAttribute = module.Import(stilettoReferences.SingletonAttribute);

            ProcessedAssemblyAttribute = module.Import(stilettoReferences.ProcessedAssemblyAttribute);
            ProcessedAssemblyAttribute_Ctor = module.Import(stilettoReferences.ProcessedAssemblyAttribute_Ctor);
        }

        private void ImportInjectBindingDictionary(ModuleDefinition module, TypeReference tDict)
        {
            var tFuncOfBinding = FuncOfT.MakeGenericInstanceType(Binding);
            var tDictOfStringToBindingFn = tDict.MakeGenericInstanceType(
                String, tFuncOfBinding);

            DictionaryOfStringToBindingFn = tDictOfStringToBindingFn;
            DictionaryOfStringToBindingFn_New =
                module.Import(
                    tDictOfStringToBindingFn.Resolve()
                                            .Methods
                                            .First(
                                                c =>
                                                c.Name == ".ctor" &&
                                                c.Parameters.Count == 1 &&
                                                c.Parameters[0].ParameterType.Name.StartsWith("IEqualityComparer")))
                      .MakeHostInstanceGeneric(String, tFuncOfBinding);
            DictionaryOfStringToBindingFn_Add =
                module.Import(tDictOfStringToBindingFn.Resolve().GetMethod("Add"))
                      .MakeHostInstanceGeneric(String, tFuncOfBinding);
            DictionaryOfStringToBindingFn_TryGetValue =
                module.Import(tDictOfStringToBindingFn.Resolve().GetMethod("TryGetValue"))
                      .MakeHostInstanceGeneric(String, tFuncOfBinding);
        }

        private void ImportRuntimeModuleDictionary(ModuleDefinition module, TypeReference tDict)
        {
            var tFuncOfModule = FuncOfT.MakeGenericInstanceType(RuntimeModule);
            var tModuleDict = tDict.MakeGenericInstanceType(Type, tFuncOfModule);
            DictionaryOfTypeToModuleFn = module.Import(tModuleDict);
            DictionaryOfTypeToModuleFn_New =
                module.Import(tModuleDict.Resolve()
                                         .GetDefaultConstructor())
                      .MakeHostInstanceGeneric(Type, tFuncOfModule);
            DictionaryOfTypeToModuleFn_Add =
                module.Import(tModuleDict.Resolve().GetMethod("Add")).MakeHostInstanceGeneric(Type, tFuncOfModule);
            DictionaryOfTypeToModuleFn_TryGetValue =
                module.Import(tModuleDict.Resolve().GetMethod("TryGetValue"))
                      .MakeHostInstanceGeneric(Type, tFuncOfModule);
        }

        private void ImportProviderBindingDictionary(ModuleDefinition module, TypeReference tDict)
        {
            var tFuncOfProviderBinding = FuncOfT5.MakeGenericInstanceType(String, Object, Boolean, String, Binding);
            var tProviderDict = tDict.MakeGenericInstanceType(String, tFuncOfProviderBinding);
            DictionaryOfStringToProviderBindingFn = tProviderDict;
            DictionaryOfStringToProviderBindingFn_New =
                module.Import(tProviderDict.Resolve()
                                           .GetConstructors()
                                           .First(
                                               c =>
                                               c.Parameters.Count == 1 &&
                                               c.Parameters[0].ParameterType.Name.StartsWith("IEqualityComparer")))
                      .MakeHostInstanceGeneric(String, tFuncOfProviderBinding);
            DictionaryOfStringToProviderBindingFn_Add =
                module.Import(tProviderDict.Resolve().GetMethod("Add"))
                      .MakeHostInstanceGeneric(String, tFuncOfProviderBinding);
            DictionaryOfStringToProviderBindingFn_TryGetValue =
                module.Import(tProviderDict.Resolve().GetMethod("TryGetValue"))
                      .MakeHostInstanceGeneric(String, tFuncOfProviderBinding);
        }

        private void ImportLazyBindingDictionary(ModuleDefinition module, TypeReference tDict)
        {
            var tFuncOfLazyBinding = FuncOfT4.MakeGenericInstanceType(String, Object, String, Binding);
            var tLazyBindingDict = tDict.MakeGenericInstanceType(String, tFuncOfLazyBinding);
            DictionaryOfStringToLazyBindingFn = module.Import(tLazyBindingDict);
            DictionaryOfStringToLazyBindingFn_New =
                module.Import(tLazyBindingDict.Resolve()
                                              .GetConstructors()
                                              .First(
                                                  c =>
                                                  c.Parameters.Count == 1 &&
                                                  c.Parameters[0].ParameterType.Name.StartsWith("IEqualityComparer")))
                      .MakeHostInstanceGeneric(String, tFuncOfLazyBinding);
            DictionaryOfStringToLazyBindingFn_Add =
                module.Import(tLazyBindingDict.Resolve().GetMethod("Add"))
                      .MakeHostInstanceGeneric(String, tFuncOfLazyBinding);
            DictionaryOfStringToLazyBindingFn_TryGetValue =
                module.Import(tLazyBindingDict.Resolve().GetMethod("TryGetValue"))
                      .MakeHostInstanceGeneric(String, tFuncOfLazyBinding);
        }
    }
}
