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
using System.Linq;
﻿using Abra.Fody.Generators;
﻿using Abra.Internal;

namespace Abra.Fody.Validation
{
    public class CompilerPlugin : IPlugin
    {
        private readonly IDictionary<string, CompilerBinding> bindings;
        private readonly IDictionary<string, CompilerParameterizedBinding> lazyBindings;
        private readonly IDictionary<string, CompilerParameterizedBinding> providerBindings;

        public CompilerPlugin(
            IEnumerable<InjectBindingGenerator> bindings,
            IEnumerable<LazyBindingGenerator> lazyBindings,
            IEnumerable<ProviderBindingGenerator> providerBindings)
        {
            var comparer = StringComparer.Ordinal;
            this.bindings = bindings.ToDictionary(b => b.Key, b => new CompilerBinding(b), comparer);
            this.lazyBindings = lazyBindings.ToDictionary(b => b.Key, b => new CompilerParameterizedBinding(b), comparer);
            this.providerBindings = providerBindings.ToDictionary(b => b.Key, b => new CompilerParameterizedBinding(b), comparer);
        }

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            CompilerBinding binding;
            if (!bindings.TryGetValue(key, out binding)) {
                throw new BindingException("No binding for " + key);
            }
            return binding;
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            CompilerParameterizedBinding binding;
            if (!lazyBindings.TryGetValue(key, out binding)) {
                throw new BindingException("No lazy binding for " + key);
            }
            return binding;
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            CompilerParameterizedBinding binding;
            if (!providerBindings.TryGetValue(key, out binding)) {
                throw new BindingException("No IProvider binding for " + key);
            }
            return binding;
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            throw new NotSupportedException("Compile-time validation should never call IPlugin.GetRuntimeModule().");
        }
    }
}
