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

namespace Stiletto.Internal
{
    public class Resolver
    {
        public delegate void ErrorHandler(IEnumerable<string> errors);

        private readonly Resolver baseResolver;
        private readonly ILoader loader;
        private readonly ErrorHandler handler;

        private readonly IList<string> errors = new List<string>();
        private readonly Queue<Binding> bindingsToResolve = new Queue<Binding>();
        private readonly IDictionary<string, Binding> bindings =
            new Dictionary<string, Binding>(Key.Comparer);

        private bool attachSuccess;

        public Resolver(Resolver baseResolver, ILoader loader, ErrorHandler handler)
        {
            this.baseResolver = baseResolver;
            this.loader = loader;
            this.handler = handler;
        }

        public void InstallBindings(IDictionary<string, Binding> bindingsToInstall)
        {
            foreach (var kvp in bindingsToInstall)
            {
                bindings[kvp.Key] = Scope(kvp.Value);
            }
        }

        public IDictionary<string, Binding> ResolveAllBindings()
        {
            foreach (var binding in bindings.Values)
            {
                if (!binding.IsResolved)
                {
                    bindingsToResolve.Enqueue(binding);
                }
            }

            ResolveEnqueuedBindings();
            return new Dictionary<string, Binding>(bindings, Key.Comparer);
        }

        public Binding RequestBinding(string key, object requiredBy, bool mustBeInjectable = true, bool isLibrary = false)
        {
            Binding binding = null;
            for (var resolver = this; resolver != null; resolver = resolver.baseResolver)
            {
                if (resolver.bindings.TryGetValue(key, out binding))
                {
                    if (resolver != this && !binding.IsResolved)
                    {
                        throw new InvalidOperationException("ASSERT FALSE");
                    }
                    break;
                }
            }

            if (binding == null)
            {
                var deferredBinding = new DeferredBinding(key, requiredBy, mustBeInjectable);
                deferredBinding.IsLibrary = isLibrary;
                deferredBinding.IsDependedOn = true;
                bindingsToResolve.Enqueue(deferredBinding);
                attachSuccess = false;
                return null;
            }

            if (!binding.IsResolved)
            {
                bindingsToResolve.Enqueue(binding);
            }

            binding.IsLibrary = isLibrary;
            binding.IsDependedOn = true;
            return binding;
        }

        public void ResolveEnqueuedBindings()
        {
            while (bindingsToResolve.Count > 0)
            {
                var binding = bindingsToResolve.Dequeue();

                if (binding is DeferredBinding)
                {
                    var deferredBinding = (DeferredBinding)binding;
                    var key = deferredBinding.DeferredKey;
                    var mustBeInjectable = deferredBinding.MustBeInjectable;

                    if (bindings.ContainsKey(key))
                    {
                        // We've been satisfied.
                        continue;
                    }

                    try
                    {
                        var jitBinding = CreateJitBinding(key, binding.RequiredBy, mustBeInjectable);
                        jitBinding.IsLibrary = binding.IsLibrary;
                        jitBinding.IsDependedOn = binding.IsDependedOn;
                        if (!key.Equals(jitBinding.ProviderKey) && !key.Equals(jitBinding.MembersKey))
                        {
                            throw new BindingException("Can't create binding for " + key);
                        }

                        var scopedJitBinding = Scope(jitBinding);
                        bindingsToResolve.Enqueue(scopedJitBinding);
                        AddBindingToDictionary(scopedJitBinding);
                    }
                    catch (BindingException ex)
                    {
                        errors.Add(ex.Message + " required by " + binding.RequiredBy);
                        bindings[key] = Binding.Unresolved;
                    }
                }
                else
                {
                    attachSuccess = true;
                    binding.Resolve(this);
                    if (attachSuccess)
                    {
                        binding.IsResolved = true;
                    }
                    else
                    {
                        bindingsToResolve.Enqueue(binding);
                    }
                }
            }

            try
            {
                if (errors.Count > 0)
                {
                    handler(errors);
                }
            }
            finally
            {
                errors.Clear();
            }
        }

        private void AddBindingToDictionary(Binding binding)
        {
            if (binding.ProviderKey != null)
            {
                AddBindingIfAbsent(binding, binding.ProviderKey);
            }

            if (binding.MembersKey != null)
            {
                AddBindingIfAbsent(binding, binding.MembersKey);
            }
        }

        private void AddBindingIfAbsent(Binding binding, string key)
        {
            if (!bindings.ContainsKey(key))
            {
                bindings.Add(key, binding);
            }
        }

        private Binding CreateJitBinding(string key, object requiredBy, bool mustBeInjectable)
        {
            var providerKey = Key.GetProviderKey(key);
            if (providerKey != null)
            {
                return loader.GetIProviderInjectBinding(key, requiredBy, mustBeInjectable, providerKey);
            }

            var lazyKey = Key.GetLazyKey(key);
            if (lazyKey != null)
            {
                return loader.GetLazyInjectBinding(key, requiredBy, lazyKey);
            }

            var typeName = Key.GetTypeName(key);
            if (typeName != null && !Key.IsNamed(key))
            {
                var binding = loader.GetInjectBinding(key, typeName, mustBeInjectable);
                if (binding != null)
                {
                    return binding;
                }
            }

            throw new BindingException("No binding for " + key);
        }

        private static Binding Scope(Binding binding)
        {
            if (!binding.IsSingleton)
                return binding;

            if (binding is SingletonBinding)
                throw new ArgumentException("ASSERT FALSE: If it's already a SingletonBinding, why are we here?");

            return new SingletonBinding(binding);
        }

        private sealed class DeferredBinding : Binding
        {
            private readonly string deferredKey;
            private readonly bool mustBeInjectable;

            public string DeferredKey
            {
                get { return deferredKey; }
            }

            public bool MustBeInjectable
            {
                get { return mustBeInjectable; }
            }

            public DeferredBinding(string deferredKey, object requiredBy, bool mustBeInjectable)
                : base(null, null, false, requiredBy)
            {
                this.deferredKey = deferredKey;
                this.mustBeInjectable = mustBeInjectable;
            }

            public override object Get()
            {
                throw NotSupported();
            }

            public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
            {
                throw NotSupported();
            }

            public override void InjectProperties(object target)
            {
                throw NotSupported();
            }

            private static Exception NotSupported()
            {
                return new NotSupportedException("Deferred bindings must resolve first.");
            }
        }
    }
}
