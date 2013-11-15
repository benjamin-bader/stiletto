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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stiletto.Internal.Loaders.Reflection
{
    public class ReflectionSetBinding : SetBindingBase
    {
        public static void Add(IDictionary<string, Binding> bindings, string key, Binding binding)
        {
            Binding previous;
            SetBindingBase setBinding;

            if (bindings.TryGetValue(key, out previous))
            {
                setBinding = previous as SetBindingBase;

                if (setBinding == null)
                {
                    throw new ArgumentException("Duplicates:\n" + previous + "\n" + binding);
                }

                setBinding.IsLibrary = setBinding.IsLibrary && binding.IsLibrary;
            }
            else
            {
                setBinding = new ReflectionSetBinding(key, binding.RequiredBy)
                {
                    IsLibrary = binding.IsLibrary
                };

                bindings.Add(key, setBinding);
            }

            setBinding.Contributors.Add(Resolver.Scope(binding));
        }

        private readonly Lazy<ConstructorInfo> setCtor; 

        public ReflectionSetBinding(string key, object requiredBy)
            : base(key, requiredBy)
        {
            setCtor = new Lazy<ConstructorInfo>(GetCtor);
        }

        public override object Get()
        {
            var values = Contributors.Select(binding => binding.Get());
            var ctor = setCtor.Value;
            var set = ctor.Invoke(new object[] { values });
            return set;
        }

        private ConstructorInfo GetCtor()
        {
            var elementKey = Key.GetSetElementKey(ProviderKey);
            var typeName = Key.GetTypeName(elementKey);
            var elementType = ReflectionUtils.GetType(typeName);
            var tSet = typeof (NonGenericSetImpl<>).MakeGenericType(elementType);
            return tSet.GetConstructor(new [] { typeof (IEnumerable<object>) });
        }

        private class NonGenericSetImpl<T> : HashSet<object>, ISet<T>
        {
            public NonGenericSetImpl(IEnumerable<object> elements)
                : base(elements)
            {
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                foreach (var element in this)
                {
                    yield return (T) element;
                }
            }

            void ICollection<T>.Add(T item)
            {
                throw new NotSupportedException("Immutable set.");
            }

            void ISet<T>.UnionWith(IEnumerable<T> other)
            {
                throw new NotSupportedException("Immutable set.");
            }

            void ISet<T>.IntersectWith(IEnumerable<T> other)
            {
                throw new NotSupportedException("Immutable set.");
            }

            void ISet<T>.ExceptWith(IEnumerable<T> other)
            {
                throw new NotSupportedException("Immutable set.");
            }

            void ISet<T>.SymmetricExceptWith(IEnumerable<T> other)
            {
                throw new NotSupportedException("Immutable set.");
            }

            bool ISet<T>.IsSubsetOf(IEnumerable<T> other)
            {
                return IsSubsetOf(other.Cast<object>());
            }

            bool ISet<T>.IsSupersetOf(IEnumerable<T> other)
            {
                return IsSupersetOf(other.Cast<object>());
            }

            bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other)
            {
                return IsProperSupersetOf(other.Cast<object>());
            }

            bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other)
            {
                return IsProperSubsetOf(other.Cast<object>());
            }

            bool ISet<T>.Overlaps(IEnumerable<T> other)
            {
                return Overlaps(other.Cast<object>());
            }

            bool ISet<T>.SetEquals(IEnumerable<T> other)
            {
                return SetEquals(other.Cast<object>());
            }

            bool ISet<T>.Add(T item)
            {
                throw new NotSupportedException("Immutable set.");
            }

            bool ICollection<T>.Contains(T item)
            {
                return Contains(item);
            }

            void ICollection<T>.CopyTo(T[] array, int arrayIndex)
            {
                foreach (var element in this)
                {
                    array[arrayIndex++] = (T) element;
                }
            }

            bool ICollection<T>.Remove(T item)
            {
                throw new NotSupportedException("Immutable set.");
            }

            bool ICollection<T>.IsReadOnly
            {
                get { return true; }
            }
        }
    }
}
