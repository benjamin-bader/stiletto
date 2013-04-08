using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal abstract class RuntimeModule
    {
        private readonly Type moduleType;
        private readonly string[] entryPoints;
        private readonly Type[] includes;
        private readonly bool complete;

        protected Type ModuleType
        {
            get { return moduleType; }
        }

        internal string[] EntryPoints
        {
            get { return entryPoints; }
        }

        internal Type[] Includes
        {
            get { return includes; }
        }

        internal bool IsComplete
        {
            get { return complete; }
        }

        internal object Module { get; set; }

        protected RuntimeModule(Type moduleType, string[] entryPoints, Type[] includes, bool complete)
        {
            Conditions.CheckNotNull(moduleType, "moduleType");
            Conditions.CheckNotNull(entryPoints, "entryPoints");
            Conditions.CheckNotNull(includes, "includes");

            this.moduleType = moduleType;
            this.entryPoints = entryPoints;
            this.includes = includes;
            this.complete = complete;
        }

        internal virtual void GetBindings(IDictionary<string, Binding> bindings)
        {
            
        }

        internal abstract object CreateModule();
    }
}
