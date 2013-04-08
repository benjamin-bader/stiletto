using System;
using Abra.Internal;

namespace Abra
{
    [Qualifier]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NamedAttribute : Attribute
    {
        public string Name { get; set; }

        public NamedAttribute(string name)
        {
            Name = Conditions.CheckNotNull(name, "name");
        }
    }
}
