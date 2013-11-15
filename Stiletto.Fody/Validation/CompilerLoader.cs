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
using System.Linq;
using Stiletto.Fody.Generators;
﻿using Stiletto.Internal;

namespace Stiletto.Fody.Validation
{
    public class CompilerLoader : ILoader
    {
        private readonly IDictionary<string, Binding> bindings;
        private readonly IDictionary<string, CompilerParameterizedBinding> lazyBindings;
        private readonly IDictionary<string, CompilerParameterizedBinding> providerBindings;

        public bool HasError { get; private set; }

        public CompilerLoader(
            IEnumerable<InjectBindingGenerator> bindings,
            IEnumerable<LazyBindingGenerator> lazyBindings,
            IEnumerable<ProviderBindingGenerator> providerBindings,
            IErrorReporter errorReporter)
        {
            var comparer = StringComparer.Ordinal;

            this.bindings = new Dictionary<string, Binding>(comparer);
            foreach (var gen in bindings)
            {
                try
                {
                    this.bindings.Add(gen.Key, new CompilerBinding(gen));
                }
                catch (ArgumentException)
                {
                    const string format = "The inject binding {0} is erroneously duplicated - please report this, with a repro if possible!";
                    errorReporter.LogError(string.Format(format, gen.Key));
                    HasError = true;
                }
            }

            this.lazyBindings = new Dictionary<string, CompilerParameterizedBinding>(comparer);
            foreach (var gen in lazyBindings)
            {
                try
                {
                    this.lazyBindings.Add(gen.Key, new CompilerParameterizedBinding(gen));
                }
                catch (ArgumentException)
                {
                    var message = string.Format("The lazy binding {0} is erroneously duplicated - please report this, with a repro if possible!", gen.Key);
                    errorReporter.LogError(message);
                    HasError = true;
                }
            }

            this.providerBindings = new Dictionary<string, CompilerParameterizedBinding>(comparer);
            foreach (var gen in providerBindings)
            {
                try
                {
                    this.providerBindings.Add(gen.Key, new CompilerParameterizedBinding(gen));
                }
                catch (ArgumentException)
                {
                    var message = string.Format("The IProvider binding {0} is erroneously duplicated - please report this, with a repro if possible!", gen.Key);
                    errorReporter.LogError(message);
                    HasError = true;
                }
            }
        }

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            Binding binding;
            bindings.TryGetValue(className, out binding);
            return binding;
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            CompilerParameterizedBinding binding;
            lazyBindings.TryGetValue(key, out binding);
            return binding;
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            CompilerParameterizedBinding binding;
            providerBindings.TryGetValue(key, out binding);
            return binding;
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            throw new NotSupportedException("Compile-time validation should never call ILoader.GetRuntimeModule().");
        }
    }
}
