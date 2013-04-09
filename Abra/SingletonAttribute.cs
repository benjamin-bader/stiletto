using System;

namespace Abra
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, Inherited = false)]
    public class SingletonAttribute : Attribute
    {
    }
}
