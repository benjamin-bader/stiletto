using System;

namespace Abra
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class SingletonAttribute : Attribute
    {
    }
}
