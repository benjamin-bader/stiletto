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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Stiletto.Internal;

namespace Stiletto.Fody.Validation
{
    public class GraphWriter
    {
        private static readonly Regex typeNameReplace = new Regex(@"[.\w_]+\.([\w`_]+)", RegexOptions.Compiled);
        private static readonly Regex genericArgCountReplace = new Regex("`\\d", RegexOptions.Compiled);

        public void Write(DotWriter dotWriter, IDictionary<string, Binding> allBindings)
        {
            var nameIndex = GetNodeNames(allBindings);

            dotWriter.BeginGraph("cluster", "true");
            foreach (var kvp in nameIndex)
            {
                var binding = kvp.Key;
                var sourceName = kvp.Value;
                var dependencies = new HashSet<Binding>();
                binding.GetDependencies(dependencies, dependencies);

                foreach (var dependency in dependencies)
                {
                    var targetName = nameIndex[dependency];
                    dotWriter.WriteEdge(sourceName, targetName);
                }
            }
            dotWriter.EndGraph();
        }

        /// <summary>
        /// Constructs a mapping from bindings to unique node names, preferring
        /// names with namespaces and generic argument counts removed.
        /// </summary>
        /// <param name="bindings">
        /// A dictionary of keys to bindings.
        /// </param>
        /// <returns>
        /// Returns a dictionary of bindings to node names.
        /// </returns>
        private IDictionary<Binding, string> GetNodeNames(IDictionary<string, Binding> bindings)
        {
            var nameToNode = new Dictionary<string, Binding>();
            var collisions = new HashSet<Binding>();

            foreach (var kvp in bindings)
            {
                var key = kvp.Key;
                var binding = kvp.Value;
                var trimmedName = TrimLabel(key);

                if (nameToNode.ContainsKey(trimmedName))
                {
                    collisions.Add(nameToNode[trimmedName]);
                    collisions.Add(binding);
                }
                else
                {
                    nameToNode[trimmedName] = binding;
                }
            }

            foreach (var kvp in bindings)
            {
                var binding = kvp.Value;

                if (collisions.Contains(binding))
                {
                    var key = kvp.Key;
                    var trimmedName = TrimLabel(key);
                    nameToNode.Remove(trimmedName);
                    nameToNode.Add(key, binding);
                }
            }

            var index = new Dictionary<Binding, string>();
            foreach (var kvp in nameToNode)
            {
                var name = kvp.Key.Replace(CompilerKeys.MemberKeyPrefix, string.Empty);
                index[kvp.Value] = name;
            }

            return index;
        }

        /// <summary>
        /// Removes namespaces and generic-parameter-counts from keys.
        /// </summary>
        /// <example>
        /// For example, <c>System.Collections.Generic.IDictionary`2&lt;System.String, System.Boolean&gt;</c>
        /// becomes <c>IDictionary&lt;String, Boolean&gt;</c>.
        /// </example>
        private static string TrimLabel(string label)
        {
            var startOfType = label.IndexOf('/');

            if (startOfType >= 0)
            {
                ++startOfType;
            }

            var typeName = startOfType >= 0 ? label.Substring(startOfType) : label;
            var trimmedType = typeNameReplace.Replace(typeName, "$1");

            trimmedType = genericArgCountReplace.Replace(trimmedType, string.Empty);

            return startOfType < 0
                       ? trimmedType
                       : label.Substring(0, startOfType) + trimmedType;

        }
    }
}
