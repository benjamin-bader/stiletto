using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

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

        public abstract void Validate(IWeaver weaver);

        public abstract void Generate(IWeaver weaver);
    }
}
