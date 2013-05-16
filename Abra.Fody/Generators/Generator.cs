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
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Abra.Fody.Generators
{
    public abstract class Generator
    {
        private readonly ModuleDefinition moduleDefinition;
        private readonly References references;

        public ModuleDefinition ModuleDefinition
        {
            get { return moduleDefinition; }
        }

        protected References References
        {
            get { return references; }
        }

        protected Generator(ModuleDefinition moduleDefinition, References references)
        {
            this.moduleDefinition = Conditions.CheckNotNull(moduleDefinition, "moduleDefinition");
            this.references = Conditions.CheckNotNull(references, "references");
        }

        protected TypeReference Import(Type t)
        {
            return ModuleDefinition.Import(t);
        }

        protected FieldReference Import(FieldInfo fi)
        {
            return ModuleDefinition.Import(fi);
        }

        protected TypeReference Import(TypeReference t)
        {
            return ModuleDefinition.Import(t);
        }

        protected MethodReference Import(MethodReference m)
        {
            return ModuleDefinition.Import(m);
        }

        protected MethodReference Import(MethodBase mb)
        {
            return ModuleDefinition.Import(mb);
        }

        /// <summary>
        /// Imports a method of a generic type.
        /// </summary>
        /// <returns>
        /// Returns an imported method with the given generic arguments applied to the declaring type.
        /// </returns>
        /// <param name="t">The type declaring the desired method.</param>
        /// <param name="predicate">A predicate identifying the desired method.  Must match one and only one method.</param>
        /// <param name="genericArguments">The generic arguments to be applied.</param>
        protected MethodReference ImportGeneric(TypeReference t, Func<MethodDefinition, bool> predicate, params TypeReference[] genericArguments)
        {
            var gt = t.MakeGenericInstanceType(genericArguments);
            return ModuleDefinition
                .Import(gt.Resolve().Methods.FirstOrDefault(predicate))
                .MakeHostInstanceGeneric(genericArguments);
        }

        public abstract void Validate(IErrorReporter errorReporter);
        public abstract TypeDefinition Generate(IErrorReporter errorReporter);
        public abstract KeyedCtor GetKeyedCtor();
    }
}
