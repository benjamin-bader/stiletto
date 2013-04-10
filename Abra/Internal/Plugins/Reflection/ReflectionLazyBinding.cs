using System;

namespace Abra.Internal.Plugins.Reflection
{
    internal class ReflectionLazyBinding : Binding
    {
        private static readonly Type LAZY_TYPE = typeof (Lazy<>);
        private static readonly Type HACK_TYPE = typeof (HackAround<>);

        private readonly string lazyKey;
        private readonly Type lazyType;
        private Binding delegateBinding;
        private object delayedGet; 

        public ReflectionLazyBinding(string key, object requiredBy, string lazyKey)
            : base(key, null, false, requiredBy)
        {
            this.lazyKey = lazyKey;
            this.lazyType = Type.GetType(Key.GetTypeName(lazyKey));
        }

        internal override void Resolve(Resolver resolver)
        {
            delegateBinding = resolver.RequestBinding(lazyKey, RequiredBy);
        }

        internal override void InjectProperties(object target)
        {
            throw new NotSupportedException("Lazy property injection is not supported.");
        }

        internal override object Get()
        {
            if (delayedGet == null) {
                // So here's how it works.
                // We're returning a Lazy<T>, but we don't know at compile-time what
                // T is.  So at runtime, we have to use reflection magic to get the
                // correct type.  We can't runtime-cast delegates using Convert.ChangeType,
                // because they don't implement IConvertible.  We need to, though, because
                // at best we can offer a Func<object> to the Lazy<T> constructor, which
                // fails to resolve.  So we end-run around the type system by getting the
                // right kind of Func from HACK_TYPE, which we *can* instantiate with a
                // Func<object>.
                //
                // The moral of the story is that you should use the compiler, when it's done.
                var hackType = HACK_TYPE.MakeGenericType(lazyType);
                var hackProp = hackType.GetProperty("TypedFunc");
                Func<object> factory = () => delegateBinding.Get();
                var hack = Activator.CreateInstance(hackType, new object[] {factory});

                var concreteLazyType = LAZY_TYPE.MakeGenericType(lazyType);
                delayedGet = Activator.CreateInstance(
                    concreteLazyType, new[] { hackProp.GetValue(hack, null) });
            }

            return delayedGet;
        }

        private class HackAround<T>
        {
            private readonly Func<object> func;

            public Func<T> TypedFunc
            {
                get { return () => (T) func(); }
            }

            public HackAround(Func<object> func)
            {
                this.func = func;
            }
        }
    }
}
