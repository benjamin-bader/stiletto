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
using System.Collections.Generic;

using Stiletto.Internal;

namespace Stiletto
{
    /// <summary>
    /// The registration point for source-generated per-assembly loaders. Each
    /// compiled assembly emits a <c>[ModuleInitializer]</c> that calls
    /// <see cref="Register"/> as the assembly is first touched, so
    /// <see cref="Container.Create(object[])"/> can consult the compiled loaders
    /// directly — no <see cref="AppDomain"/> scanning or reflection required.
    ///
    /// Also usable as an explicit escape hatch for assemblies that are never
    /// referenced in code (pure runtime plugins), whose module initializer would
    /// otherwise not fire in time.
    /// </summary>
    public static class LoaderRegistry
    {
        private static readonly List<ILoader> loaders = [];

        /// <summary>Registers a loader. Called by generated module initializers.</summary>
        public static void Register(ILoader loader)
        {
            ArgumentNullException.ThrowIfNull(loader, nameof(loader));

            lock (loaders)
            {
                loaders.Add(loader);
            }
        }

        /// <summary>A point-in-time copy of the registered loaders.</summary>
        internal static ILoader[] Snapshot()
        {
            lock (loaders)
            {
                return [.. loaders];
            }
        }
    }
}
