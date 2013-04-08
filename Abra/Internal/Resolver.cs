using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal class Resolver
    {
        private readonly Resolver baseResolver;
        private readonly IPlugin plugin;
        private readonly Queue<Binding> bindingsToResolve = new Queue<Binding>();
        private readonly IDictionary<string, Binding> bindings =
            new Dictionary<string, Binding>(StringComparer.Ordinal);

        private bool attachSuccess;

        internal Resolver(Resolver baseResolver, IPlugin plugin)
        {
            this.baseResolver = baseResolver;
            this.plugin = plugin;
        }

        internal IDictionary<string, Binding> ResolveAllBindings()
        {
            var dict = new Dictionary<string, Binding>();
            
            foreach (var kvp in bindings)
            {
                if (!kvp.Value.IsResolved)
                {
                    bindingsToResolve.Enqueue(kvp.Value);
                }

                dict.Add(kvp.Key, kvp.Value);
            }

            ResolveEnqueuedBindings();
            return dict;
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

            public override object Get()
            {
                throw NotSupported();
            }

            public override void GetDependencies(ISet<Binding> getDependencies, ISet<Binding> propertyDependencies)
            {
                throw NotSupported();
            }

            public override void InjectProperties(object target)
            {
                throw NotSupported();
            }

            private Exception NotSupported()
            {
                return new NotSupportedException("Deferred bindings must resolve first.");
            }
        }
    }
}
