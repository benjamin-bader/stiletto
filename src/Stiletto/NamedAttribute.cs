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
    /// Represents a named dependency.
    /// </summary>
    /// <remarks>
    /// Naming a dependency allows you to provide multiple different values
    /// of the same type, which is often useful any but trivial programs.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Module]
    /// public class MyModule
    /// {
    ///     [Provides, Named("days")]
    ///     public IList&lt;string&gt; GetWeekdays()
    ///     {
    ///         return new[] { "Monday", "Tuesday", "etc." };
    ///     }
    ///
    ///     [Provides, Named("months"]
    ///     public IList&lt;string&gt; GetMonths()
    ///     {
    ///         return new[] { "Jan", "Feb", "Mar", "Etc" };
    ///     }
    /// }
    ///
    /// public class NeedsStringLists
    /// {
    ///     [Inject, Named("days")] public IList&lt;string&gt; Days { get; set; }
    ///     [Inject, Named("months")] public IList&lt;string&gt; Months { get; set; }
    /// }
    /// </code>
    /// </example>

    [Qualifier]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property)]
    public class NamedAttribute : Attribute
    {
        public string Name { get; private set; }

        public NamedAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            Name = name;
        }

        public override string ToString()
        {
            return "{" + Name + "}";
        }
    }
}
