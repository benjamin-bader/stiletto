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
using System.Diagnostics;

namespace Stiletto.Internal
{
    internal static class Conditions
    {
        [Conditional("ASSERTIONS")]
        internal static void CheckArgument(bool condition, string message = "", params object[] args)
        {
            if (condition) return;

            if (args.Length > 0)
            {
                message = string.Format(message, args);
            }

            throw new ArgumentException(message);
        }

        [Conditional("ASSERTIONS")]
        internal static void CheckNotNull<T>(T value, string name = null)
            where T : class
        {
            if (!ReferenceEquals(value, null))
            {
                return;
            }

            throw new ArgumentNullException(name ?? "value");
        }
    }
}
