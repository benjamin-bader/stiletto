using System;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler
{
    public static class Attributes
    {
        public static bool IsNamedAttribute(IAttribute attr)
        {
            return AttributeHasName(attr, Constants.NamedAttributeName);
        }

        public static bool IsSingletonAttribute(IAttribute attr)
        {
            return AttributeHasName(attr, Constants.SingletonAttributeName);
        }

        public static bool IsProvidesAttribute(IAttribute attr)
        {
            return AttributeHasName(attr, Constants.ProvidesAttributeName);
        }

        public static bool IsInjectAttribute(IAttribute attr)
        {
            return AttributeHasName(attr, Constants.InjectAttributeName);
        }

        public static bool IsModuleAttribute(IAttribute attr)
        {
            return AttributeHasName(attr, Constants.ModuleAttributeName);
        }

        private static bool AttributeHasName(IAttribute attr, string name)
        {
            return attr.AttributeType.FullName.Equals(name, StringComparison.Ordinal);
        }
    }
}
