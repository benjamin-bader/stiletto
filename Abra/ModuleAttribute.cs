using System;

namespace Abra
{
    /// <summary>
    /// Marks a class as an Abra module.  Any public methods marked with
    /// a <see cref="ProvidesAttribute"/> will be used to satisfy dependencies.
    /// </summary>
    /// <remarks>
    /// Currently, module classes may not inherit from anything other than
    /// <see cref="System.Object"/>.  They must also expose a public default
    /// constructor.  The classes themselves are not required to be public.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ModuleAttribute : Attribute
    {
        private bool complete = true;
        private Type[] entryPoints;
        private Type[] includedModules;

        /// <summary>
        /// Gets or sets the list of types that can be directly obtained from a
        /// <see cref="Container"/> that includes this module.
        /// </summary>
        /// <remarks>
        /// The term 'entry point' does not refer to methods (as in
        /// <code>public static int Main()</code>, but to entry points into the
        /// object graph constructed by the container.  They are the starting
        /// points from which the dependency graph is constructed.
        /// </remarks>
        public Type[] EntryPoints
        {
            get { return entryPoints ?? Type.EmptyTypes; }
            set { entryPoints = value; }
        }

        /// <summary>
        /// Gets or sets the list of sub-modules included into this module.
        /// </summary>
        public Type[] IncludedModules
        {
            get { return includedModules ?? Type.EmptyTypes; }
            set { includedModules = value; }
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether this module has any outside
        /// dependencies, such as provider methods that require injected parameters.
        /// </summary>
        public bool IsComplete
        {
            get { return complete; }
            set { complete = value; }
        }
    }
}
