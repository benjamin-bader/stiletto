using Mono.Cecil;

namespace Abra.Fody
{
    public static class AttributesExtensions
    {
        public static bool IsVisible(this MethodAttributes attrs)
        {
            return (attrs & MethodAttributes.Public) == MethodAttributes.Public
                || (attrs & MethodAttributes.FamORAssem) == MethodAttributes.FamORAssem
                || (attrs & MethodAttributes.Assembly) == MethodAttributes.Assembly;
        }
    }
}
