using System;

namespace Abra
{
    /// <summary>
    /// Marks a constructor or a public property as targets for dependency
    /// injection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Property, Inherited = false)]
    public class InjectAttribute : Attribute
    {
    }
}
