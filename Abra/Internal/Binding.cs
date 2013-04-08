using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal abstract class Binding
    {
        public static readonly Binding Unresolved = new UnresolvedBinding();

        [Flags]
        private enum BindingState
        {
            IsSingleton = 1,
            IsResolved = 2,
            IsVisiting = 4,
            IsCycleFree = 8
        }

        private readonly string providerKey;
        private readonly string membersKey;
        private readonly object requiredBy;

        private BindingState state;

        internal bool IsSingleton
        {
            get { return (state & BindingState.IsSingleton) == BindingState.IsSingleton; }
        }

        internal virtual bool IsResolved
        {
            get { return (state & BindingState.IsResolved) == BindingState.IsResolved; }
            set
            {
                state = value
                    ? (state | BindingState.IsResolved)
                    : (state & ~BindingState.IsResolved);
            }
        }

        internal bool IsVisiting
        {
            get { return (state & BindingState.IsVisiting) == BindingState.IsVisiting; }
            set
            {
                state = value
                    ? (state | BindingState.IsVisiting)
                    : (state & ~BindingState.IsVisiting);
            }
        }

        internal bool IsCycleFree
        {
            get { return (state & BindingState.IsCycleFree) == BindingState.IsCycleFree; }
            set
            {
                state = value
                    ? (state | BindingState.IsCycleFree)
                    : (state & ~BindingState.IsCycleFree);
            }
        }

        internal string ProviderKey
        {
            get { return providerKey; }
        }

        internal string MembersKey
        {
            get { return membersKey; }
        }

        internal object RequiredBy
        {
            get { return requiredBy; }
        }

        protected Binding(string providerKey, string membersKey, bool isSingleton, object requiredBy)
        {
            this.providerKey = providerKey;
            this.membersKey = membersKey;
            this.state = isSingleton ? BindingState.IsSingleton : 0;
            this.requiredBy = requiredBy;
        }

        public virtual object Get()
        {
            throw new InvalidOperationException("This method must be overridden.");
        }

        public virtual void InjectProperties(object target)
        {
            // no-op
        }

        public virtual void GetDependencies(ISet<Binding> getDependencies, ISet<Binding> propertyDependencies)
        {
            // no-op.
        }

        public virtual void Resolve(Resolver resolver)
        {
            // no-op
        }

        private class UnresolvedBinding : Binding
        {
            internal UnresolvedBinding()
                : base(null, null, false, null)
            {}

            public override object Get()
            {
                throw new InvalidOperationException("Don't `Get` and unresolved binding");
            }
        }
    }
}
