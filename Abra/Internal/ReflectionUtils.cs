using System;

namespace Abra.Internal
{
    internal class ReflectionUtils
    {
        public static Type GetType(string fullName)
        {
            var t = Type.GetType(fullName, false);

            if (t != null) {
                return t;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                t = assembly.GetType(fullName, false);
                
                if (t != null) {
                    break;
                }
            }

            return t;
        }
    }
}
