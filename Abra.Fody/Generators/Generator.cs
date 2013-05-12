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

        public ModuleDefinition ModuleDefinition
        {
            get { return moduleDefinition; }
        }

        protected Generator(ModuleDefinition moduleDefinition)
        {
            if (moduleDefinition == null) {
                throw new ArgumentNullException("moduleDefinition");
            }

            this.moduleDefinition = moduleDefinition;
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

        public abstract void Validate(IWeaver weaver);
        public abstract TypeDefinition Generate(IWeaver weaver);
		public abstract KeyedCtor GetKeyedCtor();
    }
}
