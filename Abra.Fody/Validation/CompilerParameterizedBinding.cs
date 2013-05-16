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
using Abra.Fody.Generators;
using Abra.Internal;

namespace Abra.Fody.Validation
{
    /// <summary>
    /// Wraps both <see cref="Lazy&lt;T&gt;"/> and <see cref="IProvider&lt;T&gt;"/>
    /// bindings for post-build graph validation.
    /// </summary>
    public class CompilerParameterizedBinding : Binding
    {
        private readonly string elementKey;
        
        private Binding elementBinding;

        public CompilerParameterizedBinding(LazyBindingGenerator generator)
            : base(generator.Key, null, false, generator)
        {
            elementKey = generator.LazyKey;
        }

        public CompilerParameterizedBinding(ProviderBindingGenerator generator)
            : base(generator.Key, null, false, generator)
        {
            elementKey = generator.ProviderKey;
        }

        public override void Resolve(Resolver resolver)
        {
            elementBinding = resolver.RequestBinding(elementKey, null);
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            injectDependencies.Add(elementBinding);
        }

        public override object Get()
        {
            throw new NotSupportedException("Compiler validation should never call Binding.Get().");
        }

        public override void InjectProperties(object target)
        {
            throw new NotSupportedException("Compiler validation should never call Binding.InjectProperties(object).");
        }
    }
}
