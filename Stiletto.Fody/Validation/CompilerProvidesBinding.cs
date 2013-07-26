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
using Stiletto.Fody.Generators;
using Stiletto.Internal;

namespace Stiletto.Fody.Validation
{
    public class CompilerProvidesBinding : ProviderMethodBindingBase
    {
        private readonly ProviderMethodBindingGenerator generator;

        private IList<Binding> paramBindings;

        public CompilerProvidesBinding(ProviderMethodBindingGenerator generator)
            : base(generator.Key, null, generator.IsSingleton, generator, generator.ModuleType.FullName, generator.ProviderMethod.Name)
        {
            this.generator = generator;
            IsLibrary = generator.IsLibrary;
        }

        public override void Resolve(Resolver resolver)
        {
            paramBindings = new List<Binding>(generator.ParamKeys.Count);
            foreach (var key in generator.ParamKeys)
            {
                paramBindings.Add(resolver.RequestBinding(key, generator.ProviderMethod.FullName));
            }
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            injectDependencies.UnionWith(paramBindings);
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