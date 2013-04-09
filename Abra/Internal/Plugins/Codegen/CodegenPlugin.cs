using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal.Plugins.Codegen
{
    internal class CodegenPlugin : IPlugin
    {
        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            // TODO: Implement
            throw new NotImplementedException();
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            // TODO: Implement
            throw new NotImplementedException();
        }
    }
}
