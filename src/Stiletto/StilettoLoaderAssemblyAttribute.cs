/*
 * Copyright © Ben Bader
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
using System.ComponentModel;

namespace Stiletto
{
    /// <summary>
    /// Generator infrastructure — not intended for hand-authoring. The source
    /// generator stamps this onto every assembly that emits a
    /// <c>Stiletto.Generated.CompiledLoader</c>, recording the fully-qualified name
    /// of that assembly's public, idempotent registration entry point (its
    /// <c>EnsureRegistered</c> method's declaring type).
    ///
    /// When the generator compiles an assembly that builds containers
    /// (a <see cref="Container.Create(object[])"/> call site, or an executable),
    /// it scans the reference closure for this attribute and emits a single eager
    /// aggregate <c>[ModuleInitializer]</c> that calls each marked assembly's
    /// <c>EnsureRegistered</c> — guaranteeing every compiled loader in the closure
    /// is registered before any container snapshots the registry, without relying
    /// on incidental type-touch timing.
    /// </summary>
    /// <param name="registrationTypeName">
    /// The fully-qualified metadata name of the public static class exposing the
    /// idempotent <c>EnsureRegistered()</c> method for this assembly's loader.
    /// </param>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StilettoLoaderAssemblyAttribute(string registrationTypeName) : Attribute
    {
        /// <summary>
        /// The fully-qualified metadata name of the assembly's public
        /// <c>EnsureRegistered()</c> host type.
        /// </summary>
        public string RegistrationTypeName { get; } = registrationTypeName;
    }
}
