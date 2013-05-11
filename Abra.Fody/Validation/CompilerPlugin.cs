using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abra.Internal;
using Mono.Cecil;

namespace Abra.Fody.Validation
{
    public class CompilerPlugin : IPlugin
    {
        private IDictionary<string, TypeDefinition> bindings;
 
        public CompilerPlugin(IDictionary<string, TypeDefinition> bindings)
        {
            
        }

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            throw new NotImplementedException();
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            throw new NotImplementedException();
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            throw new NotImplementedException();
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            throw new NotImplementedException();
        }
    }
}
