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
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stiletto.Fody
{
    public class ModuleReader
    {
        public bool UsesStiletto
        {
            get { return InjectTypes.Any() || ModuleTypes.Any(); }
        }

        public IList<TypeDefinition> InjectTypes { get; private set; }
        public IList<TypeDefinition> ModuleTypes { get; private set; }

        private ModuleReader()
        {
            InjectTypes = new List<TypeDefinition>();
            ModuleTypes = new List<TypeDefinition>();
        }

        public static ModuleReader Read(ModuleDefinition module)
        {
            var reader = new ModuleReader();

            if (module.AssemblyReferences.All(reference => reference.Name != "Stiletto"))
            {
                return reader;
            }

            var allTypes = module.GetTypes();

            foreach (var t in allTypes)
            {
                if (IsModule(t))
                {
                    reader.ModuleTypes.Add(t);
                }

                if (IsInject(t))
                {
                    reader.InjectTypes.Add(t);
                }
            }

            return reader;
        }

        /// <summary>
        /// Checks if a given <paramref name="type"/> is a module.
        /// </summary>
        /// <remarks>
        /// To be a module, a type must be decorated with a [Module] attribute.
        /// </remarks>
        /// <param name="type">
        /// The possible module.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the give <paramref name="type"/>
        /// is a module, and <see langword="false"/> otherwise.
        /// </returns>
        private static bool IsModule(TypeDefinition type)
        {
            return type.HasCustomAttributes
                && type.CustomAttributes.Any(Attributes.IsModuleAttribute);
        }

        /// <summary>
        /// Checks if a given <paramref name="type"/> is injectable.
        /// </summary>
        /// <remarks>
        /// To be "injectable", a type needs to have at least one property
        /// or constructor decorated with an [Inject] attribute.
        /// </remarks>
        /// <param name="type">
        /// The possibly-injectable type.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the given <paramref name="type"/>
        /// is injectable, and <see langword="false"/> otherwise.
        /// </returns>
        private static bool IsInject(TypeDefinition type)
        {
            return type.GetConstructors().Any(c => c.CustomAttributes.Any(Attributes.IsInjectAttribute))
                || type.Properties.Any(p => p.CustomAttributes.Any(Attributes.IsInjectAttribute));
        }
    }
}
