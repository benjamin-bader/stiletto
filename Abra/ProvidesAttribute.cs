using System;

namespace Abra
{
    /// <summary>
    /// Marks a module method as providing a type.  All dependencies on this
    /// method's return type will be provided by this method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ProvidesAttribute : Attribute
    {
    }
}
