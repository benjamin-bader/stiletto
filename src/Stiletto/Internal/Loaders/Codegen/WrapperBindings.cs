/*
 * Copyright © Ben Bader
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

namespace Stiletto.Internal.Loaders.Codegen
{
    /// <summary>
    /// A compiled <see cref="Lazy{T}"/> binding. Equivalent to the reflection
    /// loader's <c>ReflectionLazyBinding</c>, but without the runtime
    /// <c>MakeGenericType</c>/<c>Activator</c> dance — the generator instantiates
    /// this with a concrete <typeparamref name="T"/>, so it is AOT-safe.
    /// </summary>
    public sealed class LazyBinding<T> : Binding
    {
        private readonly string lazyKey;
        private Binding delegateBinding = null!;
        private Lazy<T>? lazy;

        public LazyBinding(string key, object requiredBy, string lazyKey)
            : base(key, null, false, requiredBy)
        {
            this.lazyKey = lazyKey;
        }

        public override void Resolve(Resolver resolver)
        {
            delegateBinding = resolver.RequestBinding(lazyKey, RequiredBy);
        }

        public override object Get()
        {
            return lazy ??= new Lazy<T>(() => (T)delegateBinding.Get());
        }

        public override void InjectProperties(object target)
        {
            throw new NotSupportedException("Lazy property injection is not supported.");
        }
    }

    /// <summary>
    /// A compiled <see cref="IProvider{T}"/> binding — the AOT-safe counterpart of
    /// the reflection loader's <c>ReflectionProviderBinding</c>.
    /// </summary>
    public sealed class ProviderBinding<T> : Binding
    {
        private readonly bool mustBeInjectable;
        private readonly string delegateKey;
        private Binding inner = null!;
        private IProvider<T>? impl;

        public ProviderBinding(string key, object requiredBy, bool mustBeInjectable, string delegateKey)
            : base(key, null, false, requiredBy)
        {
            this.mustBeInjectable = mustBeInjectable;
            this.delegateKey = delegateKey;
        }

        public override void Resolve(Resolver resolver)
        {
            inner = resolver.RequestBinding(delegateKey, RequiredBy, mustBeInjectable);
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            inner.GetDependencies(injectDependencies, propertyDependencies);
        }

        public override void InjectProperties(object target)
        {
            inner.InjectProperties(target);
        }

        public override object Get()
        {
            return impl ??= new Impl(inner);
        }

        private sealed class Impl : IProvider<T>
        {
            private readonly Binding binding;

            public Impl(Binding binding)
            {
                this.binding = binding;
            }

            public T Get()
            {
                return (T)binding.Get();
            }
        }
    }
}
