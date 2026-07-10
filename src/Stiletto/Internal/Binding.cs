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
    public abstract class Binding
    {
        public static readonly Binding Unresolved = new UnresolvedBinding();

        [Flags]
        private enum BindingState
        {
            IsSingleton = 1,
            IsResolved = 2,
            IsVisiting = 4,
            IsCycleFree = 8,
            IsLibrary = 16,
            IsDependedOn = 32,
        }

        private readonly string providerKey;
        private readonly string membersKey;
        private readonly object requiredBy;

        private BindingState state;

        public bool IsSingleton
        {
            get { return (state & BindingState.IsSingleton) == BindingState.IsSingleton; }
        }

        public virtual bool IsResolved
        {
            get { return (state & BindingState.IsResolved) == BindingState.IsResolved; }
            set
            {
                state = value
                    ? (state | BindingState.IsResolved)
                    : (state & ~BindingState.IsResolved);
            }
        }

        public virtual bool IsVisiting
        {
            get { return (state & BindingState.IsVisiting) == BindingState.IsVisiting; }
            set
            {
                state = value
                    ? (state | BindingState.IsVisiting)
                    : (state & ~BindingState.IsVisiting);
            }
        }

        public virtual bool IsCycleFree
        {
            get { return (state & BindingState.IsCycleFree) == BindingState.IsCycleFree; }
            set
            {
                state = value
                    ? (state | BindingState.IsCycleFree)
                    : (state & ~BindingState.IsCycleFree);
            }
        }

        public virtual bool IsLibrary
        {
            get { return (state & BindingState.IsLibrary) == BindingState.IsLibrary; }
            set
            {
                state = value
                    ? (state | BindingState.IsLibrary)
                    : (state & ~BindingState.IsLibrary);
            }
        }

        public virtual bool IsDependedOn
        {
            get { return (state & BindingState.IsDependedOn) == BindingState.IsDependedOn; }
            set
            {
                state = value
                    ? (state | BindingState.IsDependedOn)
                    : (state & ~BindingState.IsDependedOn);
            }
        }

        public string ProviderKey
        {
            get { return providerKey; }
        }

        public string MembersKey
        {
            get { return membersKey; }
        }

        public object RequiredBy
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

        public abstract object Get();

        public virtual void InjectProperties(object target)
        {
            // no-op
        }

        public virtual void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            // no-op.
        }

        public virtual void Resolve(Resolver resolver)
        {
            // no-op
        }

        public override string ToString()
        {
            return ProviderKey;
        }

        private class UnresolvedBinding : Binding
        {
            public UnresolvedBinding()
                : base(null, null, false, null)
            { }

            public override object Get()
            {
                throw new InvalidOperationException("Don't `Get` an unresolved binding");
            }
        }
    }
}
