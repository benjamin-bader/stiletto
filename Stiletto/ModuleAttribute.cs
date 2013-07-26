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

namespace Stiletto
{
    /// <summary>
    /// Marks a class as an Stiletto module.  Any public methods marked with
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
        private bool library;
        private bool isOverride;
        private Type[] injects;
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
        public Type[] Injects
        {
            get { return injects ?? Type.EmptyTypes; }
            set { injects = value; }
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

        /// <summary>
        /// Gets or sets a value indicating whether this module is part of a library
        /// to be included in other projects.
        /// </summary>
        /// <remarks>
        /// A module that is marked as a library is allowed to have unsatisfied dependencies,
        /// on the assumption that they will be provided by the projects that include it.
        /// </remarks>
        public bool IsLibrary
        {
            get { return library; }
            set { library = value; }
        }

        /// <summary>
        /// Gets or set s a value indicating whether this module will override
        /// other modules.
        /// </summary>
        /// <remarks>
        /// An overriding module's provider methods will take precedence over a non-
        /// overriding module.  For example, given the following modules:
        /// <code>
        /// [Module(Injects = new[] { typeof(bool) })]
        /// public class Module
        /// {
        ///     [Provides]
        ///     public bool ProvideBoolean()
        ///     {
        ///         return false;
        ///     }
        /// }
        ///
        /// [Module(IsOverride = true)]
        /// public class OverridingModule
        /// {
        ///     [Provides]
        ///     public bool ProvideAnotherBoolean()
        ///     {
        ///         return true;
        ///     }
        /// }
        ///
        /// Container.Create(new Module(), new OverridingModule()).Get&lt;bool&gt;(); // true
        /// </code>
        ///
        /// <para>
        /// This is especially useful during testing, when you may only want to
        /// alter one or two specific injections.
        /// </para>
        ///
        /// </remarks>
        public bool IsOverride
        {
            get { return isOverride; }
            set { isOverride = value; }
        }
    }
}
