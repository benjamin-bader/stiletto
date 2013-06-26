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

namespace Stiletto.Internal.Plugins.Reflection
{
    internal class ReflectionRuntimeModule : RuntimeModule
    {
        private const BindingFlags DeclaredMethods =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        public ReflectionRuntimeModule(Type moduleType, ModuleAttribute attribute)
            : base(moduleType,
                   attribute.Injects.Select(Key.GetMemberKey).ToArray(),
                   attribute.IncludedModules,
                   attribute.IsComplete,
                   attribute.IsLibrary,
                   attribute.IsOverride)
        {
        }

        public override void GetBindings(IDictionary<string, Binding> bindings)
        {
            for (var t = ModuleType; t != typeof(object); t = t.BaseType)
            {
                // t will always be typeof(object) before it is null.
                // ReSharper disable PossibleNullReferenceException
                var methods = t.GetMethods(DeclaredMethods);
                // ReSharper restore PossibleNullReferenceException
                for (var i = 0; i < methods.Length; ++i)
                {
                    var m = methods[i];
                    if (!m.HasAttribute<ProvidesAttribute>())
                    {
                        continue;
                    }

                    var key = Key.Get(m.ReturnType, m.GetQualifierName());
                    AddNewBinding(bindings, key, m);

                }
            }
        }

        public override object CreateModule()
        {
            if (ModuleType.BaseType != typeof(object))
            {
                throw new BindingException("Modules must inherit only from System.Object.");
            }

            var ctor = ModuleType.GetConstructor(Type.EmptyTypes);

            if (ctor == null)
            {
                throw new BindingException("Modules must have a public default constructor.");
            }

            return ctor.Invoke(null);
        }

        private void AddNewBinding(IDictionary<string, Binding> bindings, string key, MethodInfo method)
        {
            bindings.Add(key, new ProviderMethodBinding(method, key, Module, IsLibrary));
        }

        private class ProviderMethodBinding : ProviderMethodBindingBase
        {
            private readonly MethodInfo method;
            private readonly object target;
            private Binding[] methodParameterBindings;

            public ProviderMethodBinding(MethodInfo method, string providerKey, object target, bool isLibrary)
                : base(providerKey, null, method.HasAttribute<SingletonAttribute>(), method,
                       method.DeclaringType.FullName, method.Name)
            {
                this.method = method;
                this.target = target;

                IsLibrary = isLibrary;
            }

            public override void Resolve(Resolver resolver)
            {
                var parameters = method.GetParameters();

                methodParameterBindings = new Binding[parameters.Length];
                for (var i = 0; i < parameters.Length; ++i)
                {
                    var key = Key.Get(parameters[i].ParameterType, parameters[i].GetQualifierName());
                    methodParameterBindings[i] = resolver.RequestBinding(key, target.GetType().FullName + "::" + method.Name);
                }
            }

            public override object Get()
            {
                var args = new object[methodParameterBindings.Length];
                for (var i = 0; i < methodParameterBindings.Length; ++i)
                {
                    args[i] = methodParameterBindings[i].Get();
                }

                return method.Invoke(target, args);
            }

            public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
            {
                injectDependencies.UnionWith(methodParameterBindings);
            }

            public override void InjectProperties(object instance)
            {
                throw new NotSupportedException("Provider methods can't inject members.");
            }
        }
    }
}
