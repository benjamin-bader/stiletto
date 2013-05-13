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

namespace Abra.Internal
{
    public abstract class Binding : Visitable
    {
        public static readonly Binding Unresolved = new UnresolvedBinding();

        [Flags]
        private enum BindingState
        {
            IsSingleton = 1,
            IsResolved = 2,
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

        private class UnresolvedBinding : Binding
        {
            public UnresolvedBinding()
                : base(null, null, false, null)
            {}

            public override object Get()
            {
                throw new InvalidOperationException("Don't `Get` and unresolved binding");
            }
        }
    }
}
