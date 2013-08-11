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

namespace Stiletto.Internal.Loaders.Codegen
{
    public static class SetBindings
    {
        public static void Add<T>(IDictionary<string, Binding> bindings, string key, Binding binding)
        {
            Binding previous;
            SetBinding<T> setBinding;

            if (bindings.TryGetValue(key, out previous))
            {
                setBinding = previous as SetBinding<T>;
                if (setBinding == null)
                {
                    throw new InvalidOperationException("Duplicates:\n" + previous + "\n" + binding);
                }
            }
            else
            {
                bindings[key] = setBinding = new SetBinding<T>(key, binding.RequiredBy);
            }

            setBinding.Contributors.Add(Resolver.Scope(binding));
        }
    }

    public class SetBinding<T> : SetBindingBase
    {
        public SetBinding(string key, object requiredBy)
            : base(key, requiredBy)
        {
        }

        public override object Get()
        {
            return new ReadOnlyHashSet<T>(Contributors.Select(binding => (T) binding.Get()));
        }
    }
}
