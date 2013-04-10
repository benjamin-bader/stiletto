using System;

namespace Abra
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
