using System;

namespace Abra
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ModuleAttribute : Attribute
    {
        private Type[] entryPoints;
        private Type[] includedModules;

        public Type[] EntryPoints
        {
            get { return entryPoints ?? Type.EmptyTypes; }
            set { entryPoints = value; }
        }

        public Type[] IncludedModules
        {
            get { return includedModules ?? Type.EmptyTypes; }
            set { includedModules = value; }
        }
        
        public bool IsComplete { get; set; }
    }
}
