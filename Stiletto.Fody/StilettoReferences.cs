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
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stiletto.Fody
{
    /// <summary>
    /// Contains resolved types and methods from the common Stiletto library.
    /// </summary>
    public class StilettoReferences
    {
        // ReSharper disable InconsistentNaming

        public TypeDefinition Binding { get; private set; }
        public MethodDefinition Binding_Ctor { get; private set; }
        public MethodDefinition Binding_GetDependencies { get; private set; }
        public MethodDefinition Binding_Resolve { get; private set; }
        public MethodDefinition Binding_Get { get; private set; }
        public MethodDefinition Binding_InjectProperties { get; private set; }
        public MethodDefinition Binding_RequiredBy_Getter { get; private set; }
        public MethodDefinition Binding_IsLibrary_Setter { get; private set; }

        public TypeDefinition SetBindings { get; private set; }
        public MethodDefinition SetBindings_Add { get; private set; }

        public TypeDefinition RuntimeModule { get; private set; }
        public MethodDefinition RuntimeModule_Ctor { get; private set; }
        public MethodDefinition RuntimeModule_Module_Getter { get; private set; }

        public TypeDefinition Container { get; private set; }
        public MethodDefinition Container_Create { get; private set; }
        public MethodDefinition Container_CreateWithLoaders { get; private set; }

        public TypeDefinition ILoader { get; private set; }
        public MethodDefinition ILoader_GetInjectBinding { get; private set; }
        public MethodDefinition ILoader_GetLazyInjectBinding { get; private set; }
        public MethodDefinition ILoader_GetIProviderInjectBinding { get; private set; }
        public MethodDefinition ILoader_GetRuntimeModue { get; private set; }

        public TypeDefinition Resolver { get; private set; }
        public MethodDefinition Resolver_RequestBinding { get; private set; }

        public TypeDefinition IProviderOfT { get; private set; }
        public MethodDefinition IProviderOfT_Get { get; private set; }

        public TypeDefinition InjectAttribute { get; private set; }
        public TypeDefinition ModuleAttribute { get; private set; }
        public TypeDefinition ProvidesAttribute { get; private set; }
        public TypeDefinition NamedAttribute { get; private set; }
        public TypeDefinition SingletonAttribute { get; private set; }

        public TypeDefinition ProcessedAssemblyAttribute { get; private set; }
        public MethodDefinition ProcessedAssemblyAttribute_Ctor { get; private set; }

        private StilettoReferences()
        {

        }

        /// <summary>
        /// Resolves the common Stiletto assembly, loads it into memory, and
        /// extracts types and methods so they can be imported into woven
        /// modules.
        /// </summary>
        /// <param name="assemblyResolver">
        /// The <see cref="IAssemblyResolver"/> instance provided by Fody.
        /// </param>
        /// <returns>
        /// Returns a selection of relevant types and methods from Stiletto.
        /// </returns>
        public static StilettoReferences Create(IAssemblyResolver assemblyResolver)
        {
            var stiletto = assemblyResolver.Resolve("Stiletto").MainModule;
            var types = stiletto
                .GetAllTypes()
                .Where(t => t.IsPublic)
                .ToDictionary(t => t.FullName, t => t, StringComparer.Ordinal);

            var tBinding = types["Stiletto.Internal.Binding"];
            var tBinding_ctor = tBinding.GetMethod(".ctor");
            var tBinding_GetDependencies = tBinding.GetMethod("GetDependencies");
            var tBinding_Resolve = tBinding.GetMethod("Resolve");
            var tBinding_Get = tBinding.GetMethod("Get");
            var tBinding_InjectProperties = tBinding.GetMethod("InjectProperties");
            var tBinding_RequiredBy_Getter = tBinding.GetProperty("RequiredBy").GetMethod;
            var tBinding_IsLibrary_Setter = tBinding.GetProperty("IsLibrary").SetMethod;

            var tSetBindings = types["Stiletto.Internal.Loaders.Codegen.SetBindings"];
            var tSetBindings_Add = tSetBindings.GetMethod("Add");

            var tRuntimeModule = types["Stiletto.Internal.RuntimeModule"];
            var tRuntimeModule_ctor = tRuntimeModule.GetMethod(".ctor");
            var tRuntimeModule_module_getter = tRuntimeModule.GetProperty("Module").GetMethod;

            var tContainer = types["Stiletto.Container"];
            var tContainer_Create = tContainer.GetMethod("Create");
            var tContainer_CreateWithLoaders = tContainer.GetMethod("CreateWithLoaders");

            var tLoader = types["Stiletto.Internal.ILoader"];
            var tLoader_GetInjectBinding = tLoader.GetMethod("GetInjectBinding");
            var tLoader_GetLazyInjectBinding = tLoader.GetMethod("GetLazyInjectBinding");
            var tLoader_GetProviderInjectBinding = tLoader.GetMethod("GetIProviderInjectBinding");
            var tLoader_GetRuntimeModule = tLoader.GetMethod("GetRuntimeModule");

            var tResolver = types["Stiletto.Internal.Resolver"];
            var tResolver_RequestBinding = tResolver.GetMethod("RequestBinding");

            var tProviderOfT = types["Stiletto.IProvider`1"];
            var tProviderOfT_Get = tProviderOfT.GetMethod("Get");

            var tInjectAttribute = types["Stiletto.InjectAttribute"];
            var tModuleAttribute = types["Stiletto.ModuleAttribute"];
            var tProvidesAttribute = types["Stiletto.ProvidesAttribute"];
            var tNamedAttribute = types["Stiletto.NamedAttribute"];
            var tSingletonAttribute = types["Stiletto.SingletonAttribute"];

            var tProcessedAssemblyAttribute = types["Stiletto.Internal.Loaders.Codegen.ProcessedAssemblyAttribute"];
            var tProcessedAssemblyAttribute_Ctor = tProcessedAssemblyAttribute.GetDefaultConstructor();

            return new StilettoReferences
                       {
                           Binding = tBinding,
                           Binding_Ctor = tBinding_ctor,
                           Binding_GetDependencies = tBinding_GetDependencies,
                           Binding_Resolve = tBinding_Resolve,
                           Binding_Get = tBinding_Get,
                           Binding_InjectProperties = tBinding_InjectProperties,
                           Binding_RequiredBy_Getter = tBinding_RequiredBy_Getter,
                           Binding_IsLibrary_Setter = tBinding_IsLibrary_Setter,

                           SetBindings = tSetBindings,
                           SetBindings_Add = tSetBindings_Add,

                           RuntimeModule = tRuntimeModule,
                           RuntimeModule_Ctor = tRuntimeModule_ctor,
                           RuntimeModule_Module_Getter = tRuntimeModule_module_getter,

                           Container = tContainer,
                           Container_Create = tContainer_Create,
                           Container_CreateWithLoaders = tContainer_CreateWithLoaders,

                           ILoader = tLoader,
                           ILoader_GetInjectBinding = tLoader_GetInjectBinding,
                           ILoader_GetLazyInjectBinding = tLoader_GetLazyInjectBinding,
                           ILoader_GetIProviderInjectBinding = tLoader_GetProviderInjectBinding,
                           ILoader_GetRuntimeModue = tLoader_GetRuntimeModule,

                           Resolver = tResolver,
                           Resolver_RequestBinding = tResolver_RequestBinding,

                           IProviderOfT = tProviderOfT,
                           IProviderOfT_Get = tProviderOfT_Get,

                           InjectAttribute = tInjectAttribute,
                           ModuleAttribute = tModuleAttribute,
                           ProvidesAttribute = tProvidesAttribute,
                           NamedAttribute = tNamedAttribute,
                           SingletonAttribute = tSingletonAttribute,

                           ProcessedAssemblyAttribute = tProcessedAssemblyAttribute,
                           ProcessedAssemblyAttribute_Ctor = tProcessedAssemblyAttribute_Ctor
                       };
        }
    }
}
