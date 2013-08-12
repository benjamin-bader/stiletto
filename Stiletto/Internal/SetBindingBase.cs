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
    public abstract class SetBindingBase : Binding
    {
        private readonly HashSet<Binding> contributors = new HashSet<Binding>();

        public ISet<Binding> Contributors
        {
            get { return contributors; }
        } 

        protected SetBindingBase(string key, object requiredBy)
            : base(key, null, false, requiredBy)
        {}

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            injectDependencies.UnionWith(contributors);
        }

        public override void Resolve(Resolver resolver)
        {
            foreach (var binding in contributors)
            {
                binding.Resolve(resolver);
            }
        }

        public override void InjectProperties(object target)
        {
            throw new NotSupportedException("Set bindings can't inject properties!");
        }
    }
}
