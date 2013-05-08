using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Fody
{
    public static class EnumerableExtensions
    {
        public static ISet<T> ToSet<T>(this IEnumerable<T> collection, IEqualityComparer<T> comparer = null)
        {
            return comparer == null
                ? new HashSet<T>(collection)
                : new HashSet<T>(collection, comparer);
        }
    }
}
