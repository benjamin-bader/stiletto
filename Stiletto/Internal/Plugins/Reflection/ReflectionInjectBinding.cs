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
using System.Collections.Generic;
using System.Reflection;

namespace Stiletto.Internal.Plugins.Reflection
{
    internal class ReflectionInjectBinding : Binding
    {
        private readonly Type type;
        private readonly Type baseType;
        private readonly string[] keys;
        private readonly ConstructorInfo ctor;
        private readonly Binding[] paramterBindings;
        private readonly PropertyInfo[] properties;
        private readonly Binding[] propertyBindings;
        private Binding baseTypeBinding;

        private ReflectionInjectBinding(
            string providerKey, string membersKey, bool isSingleton, Type t,
            PropertyInfo[] properties, ConstructorInfo ctor, int parameterCount,
            Type baseType, string[] keys)
            : base(providerKey, membersKey, isSingleton, t)
        {
            this.type = t;
            this.ctor = ctor;
            this.properties = properties;
            this.baseType = baseType;
            this.keys = keys;
            this.paramterBindings = new Binding[parameterCount];
            this.propertyBindings = new Binding[properties.Length];
        }

        public override void Resolve(Resolver resolver)
        {
            var k = 0;
            for (var i = 0; i < properties.Length; ++i)
            {
                if (propertyBindings[i] == null)
                {
                    propertyBindings[i] = resolver.RequestBinding(keys[k], type.FullName + "." + properties[i].Name);
                }

                ++k;
            }

            if (ctor != null)
            {
                for (var i = 0; i < paramterBindings.Length; ++i)
                {
                    if (paramterBindings[i] == null)
                    {
                        paramterBindings[i] = resolver.RequestBinding(keys[k], type.FullName + "::.ctor");
                    }
                    ++k;
                }
            }

            if (baseType != null && baseTypeBinding == null)
            {
                baseTypeBinding = resolver.RequestBinding(keys[k], MembersKey, false);
            }
        }

        public override object Get()
        {
            if (ctor == null)
            {
                throw new BindingException("Reflection bindings must have a constructor to invoke.");
            }

            var args = new object[paramterBindings.Length];
            for (var i = 0; i < paramterBindings.Length; ++i)
            {
                args[i] = paramterBindings[i].Get();
            }

            try
            {
                var result = ctor.Invoke(args);
                InjectProperties(result);
                return result;
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        public override void InjectProperties(object target)
        {
            try
            {
                for (var i = 0; i < properties.Length; ++i)
                {
                    properties[i].SetValue(target, propertyBindings[i].Get(), null);
                }

                if (baseTypeBinding != null)
                {
                    baseTypeBinding.InjectProperties(target);
                }
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            injectDependencies.UnionWith(paramterBindings);
            propertyDependencies.UnionWith(propertyBindings);

            if (baseTypeBinding != null)
            {
                propertyDependencies.Add(baseTypeBinding);
            }
        }
 
        public static ReflectionInjectBinding Create(Type t, bool mustBeInjectable)
        {
            var isSingleton = t.GetCustomAttributes(typeof (SingletonAttribute), false).Length > 0;
            var keys = new List<string>();
            var properties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var injectableProperties = new List<PropertyInfo>(properties.Length);
            for (var i = 0; i < properties.Length; ++i)
            {
                var p = properties[i];
                if (!p.HasAttribute<InjectAttribute>())
                {
                    continue;
                }

                if (p.GetSetMethod() == null)
                {
                    throw new BindingException("[Inject]-able properties must have a public setter.");
                }

                var namedAttribute = p.GetSingleAttribute<NamedAttribute>();
                var name = namedAttribute != null
                    ? namedAttribute.Name
                    : null;

                keys.Add(Key.Get(p.PropertyType, name));

                injectableProperties.Add(p);
            }

            var ctors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            ConstructorInfo injectableCtor = null;
            for (var i = 0; i < ctors.Length; ++i)
            {
                if (!ctors[i].HasAttribute<InjectAttribute>())
                {
                    continue;
                }

                if (injectableCtor != null)
                {
                    throw new BindingException("Only one constructor may be marked as [Inject]-able.");
                }

                injectableCtor = ctors[i];
            }

            if (injectableCtor == null)
            {
                if (injectableProperties.Count == 0 && mustBeInjectable)
                {
                    throw new BindingException("No injectable constructor or properties found on type " + t.FullName);
                }

                var defaultCtor = t.GetConstructor(Type.EmptyTypes);

                if (defaultCtor == null)
                {
                    throw new BindingException("No default constructor found and no constructors marked as [Inject]-able.");
                }

                injectableCtor = defaultCtor;
            }

            var parameters = injectableCtor.GetParameters();
            var baseType = t.BaseType;

            for (var i = 0; i < parameters.Length; ++i)
            {
                var parameter = parameters[i];
                var namedAttribute = parameter.GetSingleAttribute<NamedAttribute>();
                var name = namedAttribute != null
                    ? namedAttribute.Name
                    : null;

                keys.Add(Key.Get(parameter.ParameterType, name));
            }

            if (baseType != null)
            {
                if (baseType.IsFrameworkType())
                {
                    baseType = null;
                }
                else
                {
                    keys.Add(Key.GetMemberKey(baseType));
                }
            }

            var providerKey = Key.Get(t);
            var membersKey = Key.GetMemberKey(t);

            return new ReflectionInjectBinding(providerKey, membersKey, isSingleton,
                t, injectableProperties.ToArray(), injectableCtor, parameters.Length,
                baseType, keys.ToArray());
        }
    }
}
