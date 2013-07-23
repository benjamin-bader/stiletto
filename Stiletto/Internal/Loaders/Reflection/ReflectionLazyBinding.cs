/*
 * Copyright © 2013 Ben Bader
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace Stiletto.Internal.Loaders.Reflection
{
    internal class ReflectionLazyBinding : Binding
    {
        private static readonly Type IMPL_TYPE = typeof (LazyImpl<>);
        private static readonly object[] EMPTY_OBJECTS = new object[0];

        static ReflectionLazyBinding()
        {
            // Try to compensate for the MonoTouch compiler, which can't
            // correctly predict which generics need to be included in
            // the final binary.  If we try to instantiate one that it didn't
            // pick up, the app crashes.  Here we're specifying the common
            // value types, string, and object, which should cover the 90% case.
            //
            // Is it too confusing to say "no value-type Lazy<T> on iOS", or should
            // we just disallow lazy/provider reflection bindings entirely on iOS?

            new LazyImpl<int>(() => 0).GetLazyInstance();
            new LazyImpl<byte>(() => 0).GetLazyInstance();
            new LazyImpl<sbyte>(() => 0).GetLazyInstance();
            new LazyImpl<short>(() => 0).GetLazyInstance();
            new LazyImpl<ushort>(() => 0).GetLazyInstance();
            new LazyImpl<int>(() => 0).GetLazyInstance();
            new LazyImpl<uint>(() => 0).GetLazyInstance();
            new LazyImpl<long>(() => 0).GetLazyInstance();
            new LazyImpl<ulong>(() => 0).GetLazyInstance();
            new LazyImpl<string>(() => "").GetLazyInstance();
            new LazyImpl<object>(() => null).GetLazyInstance();
        }

        private readonly string lazyKey;
        private readonly Type lazyType;
        private Binding delegateBinding;
        private object delayedGet; 

        public ReflectionLazyBinding(string key, object requiredBy, string lazyKey)
            : base(key, null, false, requiredBy)
        {
#if MONOTOUCH
            throw new PlatformNotSupportedException("Reflection-based Lazy<T> bindings are not supported on MonoTouch - please use the Fody plugin.");
#endif
            this.lazyKey = lazyKey;
            this.lazyType = ReflectionUtils.GetType(Key.GetTypeName(lazyKey));
        }

        public override void Resolve(Resolver resolver)
        {
            delegateBinding = resolver.RequestBinding(lazyKey, RequiredBy);
        }

        public override void InjectProperties(object target)
        {
            throw new NotSupportedException("Lazy property injection is not supported.");
        }

        public override object Get()
        {
            if (delayedGet == null) {
                // So here's how it works.
                // We're returning a Lazy<T>, but we don't know at compile-time what
                // T is.  The Lazy<T> constructor requires a Func<T>, which we can't 
                // provide here.  LazyImpl<T>, on the other hand, takes a Func<object>,
                // which we *can* provide, and casts properly and can give us a Lazy<T>.
                // So we use a bit of reflection magic to instantiate LazyImpl<T> at
                // runtime, and get our Lazy<T> that way.
                //
                // The moral of the story is that you should use the compiler, when it's done.
                var implType = IMPL_TYPE.MakeGenericType(lazyType);
                var implGet = implType.GetMethod("GetLazyInstance");
                Func<object> factory = () => delegateBinding.Get();
                var impl = Activator.CreateInstance(implType, new object[] {factory});

                delayedGet = implGet.Invoke(impl, EMPTY_OBJECTS);
            }

            return delayedGet;
        }

        private class LazyImpl<T>
        {
            private readonly Func<T> func;

            public Lazy<T> GetLazyInstance()
            {
                return new Lazy<T>(func);
            }

            public LazyImpl(Func<object> func)
            {
                this.func = () => (T) func();
            }
        }
    }
}
