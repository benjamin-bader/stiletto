using System;

namespace Abra.Internal
{
    public class Visitable
    {
        [Flags]
        public enum VisitingState : byte
        {
            IsVisiting = 1,
            IsCycleFree = 2
        }

        private VisitingState state;

        public virtual bool IsVisiting
        {
            get { return (state & VisitingState.IsVisiting) == VisitingState.IsVisiting; }
            set
            {
                state = value
                    ? (state | VisitingState.IsVisiting)
                    : (state & ~VisitingState.IsVisiting);
            }
        }

        public virtual bool IsCycleFree
        {
            get { return (state & VisitingState.IsCycleFree) == VisitingState.IsCycleFree; }
            set
            {
                state = value
                    ? (state | VisitingState.IsCycleFree)
                    : (state & ~VisitingState.IsCycleFree);
            }
        }
    }

    public static class VisitableWrapper
    {
        public static VisitableWrapper<T> Wrap<T>(T data)
        {
            return new VisitableWrapper<T>(data);
        }
    }

    public class VisitableWrapper<T> : Visitable
    {
        private readonly T data;

        public T Data
        {
            get { return data; }
        }

        public VisitableWrapper(T data)
        {
            this.data = data;
        }
    }

    internal static class WrapperExtensions
    {
        internal static VisitableWrapper<TResult> Select<TInput, TResult>(
            this VisitableWrapper<TInput> wrapper,
            Func<TInput, TResult> selector)
        {
            return new VisitableWrapper<TResult>(selector(wrapper.Data));
        }
    }
}
