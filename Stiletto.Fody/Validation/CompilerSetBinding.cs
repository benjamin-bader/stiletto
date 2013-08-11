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
using Stiletto.Internal;

namespace Stiletto.Fody.Validation
{
    public class CompilerSetBinding : SetBindingBase
    {
        public static void Add(IDictionary<string, Binding> bindings, string key, CompilerProvidesBinding binding)
        {
            Binding previous;
            CompilerSetBinding setBinding;

            if (bindings.TryGetValue(key, out previous))
            {
                setBinding = previous as CompilerSetBinding;

                if (setBinding == null)
                {
                    throw new ValidationException("Duplicates:\n" + previous + "\n" + binding);
                }
            }
            else
            {
                bindings[key] = setBinding = new CompilerSetBinding(key, binding.RequiredBy);
            }

            setBinding.Contributors.Add(Resolver.Scope(binding));
        }

        public CompilerSetBinding(string key, object requiredBy)
            : base(key, requiredBy)
        {
        }

        public override object Get()
        {
            throw new InvalidOperationException("Compiler should never call Binding.Get()");
        }
    }
}
