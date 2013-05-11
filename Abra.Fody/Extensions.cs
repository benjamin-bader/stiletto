using System;

namespace Abra.Fody
{
    public static class Extensions
    {
        public static TResult Maybe<TInput, TResult>(this TInput input, Func<TInput, TResult> result)
            where TInput : class
            where TResult : class
        {
            if (input == null) {
                return null;
            }

            return result(input);
        }
    }
}
