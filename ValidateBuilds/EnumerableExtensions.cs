using System.Collections.Generic;

namespace ValidateBuilds
{
    public static class EnumerableExtensions
    {
        public static ISet<T> ToSet<T>(this IEnumerable<T> collection)
        {
            return ToSet(collection, EqualityComparer<T>.Default);
        }

        public static ISet<T> ToSet<T>(this IEnumerable<T> collection, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(collection, comparer);
        }
    }
}
