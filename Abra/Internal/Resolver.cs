using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal class Resolver
    {
        internal delegate void ErrorHandler(IEnumerable<string> errors);

        private readonly Resolver baseResolver;
        private readonly IPlugin plugin;
        private readonly ErrorHandler handler;

        private readonly IList<string> errors = new List<string>();
        private readonly Queue<Binding> bindingsToResolve = new Queue<Binding>();
        private readonly IDictionary<string, Binding> bindings =
            new Dictionary<string, Binding>(StringComparer.Ordinal);

        private bool attachSuccess;

        internal Resolver(Resolver baseResolver, IPlugin plugin, ErrorHandler handler)
        {
            this.baseResolver = baseResolver;
            this.plugin = plugin;
            this.handler = handler;
        }

        internal void InstallBindings(IDictionary<string, Binding> bindingsToInstall)
        {
            foreach (var kvp in bindingsToInstall)
            {
                bindings.Add(kvp.Key, Scope(kvp.Value));
            }
        }

        internal IDictionary<string, Binding> ResolveAllBindings()
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

        internal Binding RequestBinding(string key, object requiredBy, bool mustBeInjectable = true)
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
                bindingsToResolve.Enqueue(deferredBinding);
                attachSuccess = false;
                return null;
            }

            if (!binding.IsResolved)
            {
                bindingsToResolve.Enqueue(binding);
            }

            return binding;
        }

        internal void ResolveEnqueuedBindings()
        {
            while (bindingsToResolve.Count > 0)
            {
                var binding = bindingsToResolve.Dequeue();

                if (binding is DeferredBinding)
                {
                    var deferredBinding = (DeferredBinding) binding;
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
                        if (!key.Equals(jitBinding.ProviderKey) && !key.Equals(jitBinding.MembersKey))
                        {
                            var ex = new InvalidOperationException();
                            ex.Data.Add("ResolveError", "Can't create binding for " + key);
                            throw ex;
                        }

                        var scopedJitBinding = Scope(jitBinding);
                        bindingsToResolve.Enqueue(scopedJitBinding);
                        AddBindingToDictionary(scopedJitBinding);
                    }
                    catch (Exception ex)
                    {
                        var hasResolveError = ex.Data.Contains("ResolveError");

                        if (!hasResolveError)
                            throw;

                        errors.Add((string)ex.Data["ResolveError"]);
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
            var builtInKey = Key.GetBuiltInKey(key);
            if (builtInKey != null)
            {
                throw new NotImplementedException();
            }

            var lazyKey = Key.GetLazyKey(key);
            if (lazyKey != null)
            {
                return new LazyBinding(key, requiredBy, lazyKey);
            }

            var typeName = Key.GetTypeName(key);
            if (typeName != null && !Key.IsNamed(key))
            {
                var binding = plugin.GetInjectBinding(key, typeName, mustBeInjectable);
                if (binding != null)
                {
                    return binding;
                }
            }

            throw new ArgumentException("No binding for " + key);
        }

        private static Binding Scope(Binding binding)
        {
            if (!binding.IsSingleton)
                return binding;

            if (binding is SingletonBinding)
                throw new ArgumentException("ASSERT FALSE: If it's already a SingletonBinding, why are we here?");

            return new SingletonBinding(binding);
        }

        private class DeferredBinding : Binding
        {
            private readonly string deferredKey;
            private readonly bool mustBeInjectable;

            internal string DeferredKey
            {
                get { return deferredKey; }
            }

            internal bool MustBeInjectable
            {
                get { return mustBeInjectable; }
            }

            internal DeferredBinding(string deferredKey, object requiredBy, bool mustBeInjectable)
                : base(null, null, false, requiredBy)
            {
                this.deferredKey = deferredKey;
                this.mustBeInjectable = mustBeInjectable;
            }

            internal override object Get()
            {
                throw NotSupported();
            }

            internal override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
            {
                throw NotSupported();
            }

            internal override void InjectProperties(object target)
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
