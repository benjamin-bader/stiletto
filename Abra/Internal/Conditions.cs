using System;
using System.Diagnostics;

namespace Abra.Internal
{
    internal static class Conditions
    {
        [Conditional("ASSERTIONS")]
        internal static void CheckArgument(bool condition, string message = "", params object[] args)
        {
            if (condition) return;

            if (args.Length > 0)
            {
                message = string.Format(message, args);
            }

            throw new ArgumentException(message);
        }

        [Conditional("ASSERTIONS")]
        internal static T CheckNotNull<T>(T value, string name = null)
            where T : class
        {
            if (!ReferenceEquals(value, null))
            {
                return value;
            }

            throw new ArgumentNullException(name ?? "value");
        }
    }
}
