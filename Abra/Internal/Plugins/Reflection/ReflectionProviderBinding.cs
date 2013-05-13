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

namespace Abra.Internal.Plugins.Reflection
{
    internal class ReflectionProviderBinding : Binding
    {
        private readonly bool mustBeInjectable;
        private readonly string delegateKey;
        private Binding inner;
        private object impl;

        internal ReflectionProviderBinding(string providerKey, object requiredBy, bool mustBeInjectable, string delegateKey)
            : base(providerKey, null, false, requiredBy)
        {
            this.delegateKey = delegateKey;
            this.mustBeInjectable = mustBeInjectable;
        }

        public override void Resolve(Resolver resolver)
        {
            inner = resolver.RequestBinding(delegateKey, RequiredBy, mustBeInjectable);
        }

        public override void GetDependencies(System.Collections.Generic.ISet<Binding> injectDependencies, System.Collections.Generic.ISet<Binding> propertyDependencies)
        {
            inner.GetDependencies(injectDependencies, propertyDependencies);
        }

        public override void InjectProperties(object target)
        {
            inner.InjectProperties(target);
        }

        public override object Get()
        {
            return impl ?? (impl = ImplForType());
        }

        private object ImplForType()
        {
            var providedTypeName = Key.GetTypeName(delegateKey);
            var providedType = ReflectionUtils.GetType(providedTypeName);
            var providerType = typeof (ProviderImpl<>).MakeGenericType(providedType);
            Func<object> factory = () => inner.Get();
            return Activator.CreateInstance(providerType, new object[] { factory });
        }

        private class ProviderImpl<T> : IProvider<T>
        {
            private readonly Func<object> factory;

            public ProviderImpl(Func<object> factory)
            {
                this.factory = factory;
            }

            public T Get()
            {
                return (T) factory();
            }
        }
    }
}
