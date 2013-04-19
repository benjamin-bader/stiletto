using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    public abstract class RuntimeModule
    {
        private readonly Type moduleType;
        private readonly string[] entryPoints;
        private readonly Type[] includes;
        private readonly bool complete;

        protected Type ModuleType
        {
            get { return moduleType; }
        }

        public string[] EntryPoints
        {
            get { return entryPoints; }
        }

        public Type[] Includes
        {
            get { return includes; }
        }

        public bool IsComplete
        {
            get { return complete; }
        }

        public object Module { get; set; }

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

        public virtual void GetBindings(IDictionary<string, Binding> bindings)
        {
            
        }

        public abstract object CreateModule();
    }
}
