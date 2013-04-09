using System;

namespace Abra.Internal
{
    internal class LazyBinding : Binding
    {
        private readonly string lazyKey;
        private Binding delegateBinding;
        private Lazy<object> delayedGet; 

        public LazyBinding(string key, object requiredBy, string lazyKey)
            : base(key, null, false, requiredBy)
        {
            this.lazyKey = lazyKey;
        }

        internal override void Resolve(Resolver resolver)
        {
            delegateBinding = resolver.RequestBinding(lazyKey, RequiredBy);
            delayedGet = new Lazy<object>(() => delegateBinding.Get());
        }

        internal override void InjectProperties(object target)
        {
            throw new NotSupportedException("Lazy property injection is not supported.");
        }

        internal override object Get()
        {
            return delayedGet;
        }
    }
}
