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
using System.Diagnostics;

namespace Stiletto.Internal.Plugins.Reflection
{
    internal sealed class ReflectionPlugin : IPlugin
    {
        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            FailIfNoReflection();
            var t = ReflectionUtils.GetType(className);
            if (t.IsInterface)
            {
                return null;
            }

            return ReflectionInjectBinding.Create(t, mustBeInjectable);
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            FailIfNoReflection();
            return new ReflectionLazyBinding(key, requiredBy, lazyKey);
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            FailIfNoReflection();
            return new ReflectionProviderBinding(key, requiredBy, mustBeInjectable, providerKey);
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            FailIfNoReflection();
            var attr = moduleType.GetSingleAttribute<ModuleAttribute>();

            if (moduleType.BaseType != typeof(object))
            {
                throw new BindingException("Modules must inherit only from System.Object.");
            }

            return new ReflectionRuntimeModule(moduleType, attr);
        }

        [Conditional("NO_REFLECTION_PLUGIN")]
        private static void FailIfNoReflection()
        {
            throw new NotSupportedException("Reflection bindings have been disabled.");
        }
    }
}
