using System;

namespace Abra
{
    /// <summary>
    /// Marks an attribute as an Abra qualifier for use in modifying
    /// how a dependency can be satisfied.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class QualifierAttribute : Attribute
    {
    }
}
