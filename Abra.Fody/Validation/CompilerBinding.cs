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
    public class CompilerBinding : Binding
    {
        private readonly InjectBindingGenerator generator;

        private IList<Binding> ctorBindings;
        private IList<Binding> propertyBindings;
        private Binding baseTypeBinding;

        public CompilerBinding(InjectBindingGenerator generator)
            : base(generator.Key, generator.MembersKey, generator.IsSingleton, generator)
        {
            this.generator = generator;
        }

        public override void Resolve(Resolver resolver)
        {
            ctorBindings = new List<Binding>(generator.CtorParams.Count);
            foreach (var p in generator.CtorParams) {
                ctorBindings.Add(resolver.RequestBinding(p.Key, generator));
            }

            propertyBindings = new List<Binding>(generator.InjectableProperties.Count);
            foreach (var p in generator.InjectableProperties) {
                propertyBindings.Add(resolver.RequestBinding(p.Key, p));
            }

            if (generator.BaseTypeKey != null) {
                baseTypeBinding = resolver.RequestBinding(generator.BaseTypeKey, generator.BaseTypeKey, false);
            }
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            injectDependencies.UnionWith(ctorBindings);
            propertyDependencies.UnionWith(propertyBindings);

            if (baseTypeBinding != null) {
                propertyDependencies.Add(baseTypeBinding);
            }
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
