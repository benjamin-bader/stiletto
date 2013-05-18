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

﻿using System;

namespace Stiletto.Internal.Plugins.Codegen
{
    public class CodegenPlugin : IPlugin
    {
        public const string CompiledPluginNamespace = "Stiletto.Generated";
        public const string CompiledPluginName = "$CompiledPlugin$";
        public const string CompiledPluginFullName = CompiledPluginNamespace + "." + CompiledPluginName;

        public const string InjectSuffix    = "_CompiledBinding";
        public const string ModuleSuffix    = "_CompiledModule";
        public const string LazySuffix      = "_CompiledLazyBinding";
        public const string IProviderSuffix = "_CompiledProviderBinding";

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            return GetObjectOfTypeName<Binding>(className, InjectSuffix);
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            return GetObjectOfTypeName<Binding>(
                Key.GetTypeName(lazyKey),
                LazySuffix,
                new[] { key, requiredBy, lazyKey });
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            return GetObjectOfTypeName<Binding>(
                Key.GetTypeName(providerKey),
                IProviderSuffix,
                new[] { key, requiredBy, mustBeInjectable, providerKey });
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            return GetObjectOfTypeName<RuntimeModule>(moduleType.FullName, ModuleSuffix);
        }

        private T GetObjectOfTypeName<T>(string typeName, string suffix, object[] ctorArgs = null)
            where T : class
        {
            typeName += suffix;
            //typeName = typeName.Replace('+', '.');
            var t = ReflectionUtils.GetType(typeName);

            if (t == null) {
                return null;
            }

            return (T) Activator.CreateInstance(t, ctorArgs);
        }
    }
}
