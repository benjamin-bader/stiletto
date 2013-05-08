using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Fody
{
    public static class Conditions
    {
        public static T CheckNotNull<T>(T value, string name = null)
            where T : class
        {
            if (!ReferenceEquals(value, null)) {
                return value;
            }

            throw new ArgumentNullException(name ?? typeof(T).FullName);
        }
    }
}
