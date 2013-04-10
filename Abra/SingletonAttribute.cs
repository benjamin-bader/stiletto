using System;

namespace Abra
{
    /// <summary>
    /// Marks a class or a provider method as a singleton.  When dependencies
    /// are being resolved, at most one instance of the marked class or provider
    /// will be created.  The new instance or return value will be cached and used
    /// to satisfy all subsequent dependencies on this type.
    /// </summary>
    [Qualifier]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, Inherited = false)]
    public class SingletonAttribute : Attribute
    {
    }
}
