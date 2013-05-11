using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Abra.Fody.Validation
{
    public class Validator
    {
        private readonly IDictionary<string, TypeDefinition> bindings;

        public Validator(IDictionary<string, TypeDefinition> bindings)
        {
            this.bindings = Conditions.CheckNotNull(bindings, "bindings");
        }
    }
}
