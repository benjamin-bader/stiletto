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
using System.Text;

namespace Stiletto.Internal
{
    public class GraphVerifier
    {
        public void Verify(ICollection<Binding> bindings)
        {
            DetectCircularDependencies(bindings, new Stack<Binding>());
            DetectUnusedBindings(bindings);
        }

        public void DetectCircularDependencies(IEnumerable<Binding> bindings, Stack<Binding> path)
        {
            foreach (var binding in bindings) {
                if (binding.IsCycleFree) {
                    continue;
                }

                if (binding.IsVisiting) {
                    var sb = new StringBuilder("Dependency cycle detected:")
                        .AppendLine()
                        .Append(binding)
                        .AppendLine(" depends on");

                    var pathInOrder = path.Reverse().ToList();
                    var index = pathInOrder.IndexOf(binding) + 1;

                    for (var i = 0; i < pathInOrder.Count; ++i)
                    {
                        var currentItem = (index + i) % pathInOrder.Count;

                        sb.Append("\t");
                        sb.Append(i + 1);
                        sb.Append(". ");
                        sb.AppendLine(pathInOrder[currentItem].ToString());
                    }

                    throw new InvalidOperationException(sb.ToString());
                }

                binding.IsVisiting = true;
                path.Push(binding);

                try {
                    var dependencies = new HashSet<Binding>();
                    binding.GetDependencies(dependencies, dependencies);
                    DetectCircularDependencies(dependencies, path);
                    binding.IsCycleFree = true;
                }
                finally {
                    binding.IsVisiting = false;
                    path.Pop();
                }
            }
        }

        public void DetectUnusedBindings(IEnumerable<Binding> bindings)
        {
            var unusedBindings = bindings
                .Where(b => !b.IsLibrary && !b.IsDependedOn)
                .Select(CastOrUnwrapBinding)
                .ToList();

            if (unusedBindings.Count == 0) {
                return;
            }

            var sb = new StringBuilder()
                .AppendLine("The following [Provides] methods are unused,")
                .AppendLine("set 'IsLibrary = true' on their modules to suppress this error.");

            for (var i = 0; i < unusedBindings.Count; ++i) {
                sb.AppendFormat("{0}. {1}", i + 1, unusedBindings[i].ProviderMethodName)
                  .AppendLine();
            }

            throw new InvalidOperationException(sb.ToString());
        }

        private static ProviderMethodBindingBase CastOrUnwrapBinding(Binding binding)
        {
            var providerMethodBindingBase = binding as ProviderMethodBindingBase;
            if (providerMethodBindingBase != null) {
                return providerMethodBindingBase;
            }

            var singletonBinding = binding as SingletonBinding;
            if (singletonBinding != null) {
                providerMethodBindingBase = singletonBinding.DelegateBinding as ProviderMethodBindingBase;
            }

            if (providerMethodBindingBase == null) {
                throw new InvalidOperationException("WTF, how is an unused binding not a provides binding?");
            }

            return providerMethodBindingBase;
        }
    }
}
