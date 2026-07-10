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

        private readonly string lazyKey;
        private readonly Type lazyType;
        private Binding delegateBinding;
        private object delayedGet;

        public ReflectionLazyBinding(string key, object requiredBy, string lazyKey)
            : base(key, null, false, requiredBy)
        {
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
            if (delayedGet == null)
            {
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
                var impl = Activator.CreateInstance(implType, new object[] { factory });

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
