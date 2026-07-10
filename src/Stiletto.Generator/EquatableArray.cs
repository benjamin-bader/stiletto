using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Stiletto.Generator
{
    /// <summary>
    /// A thin wrapper over <see cref="ImmutableArray{T}"/> that implements
    /// structural equality. Incremental generator models must be value-equatable
    /// for the pipeline to cache correctly; a bare <see cref="ImmutableArray{T}"/>
    /// compares by reference and would defeat caching.
    /// </summary>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> array;

        public EquatableArray(ImmutableArray<T> array)
        {
            this.array = array;
        }

        public int Count => array.IsDefault ? 0 : array.Length;

        public T this[int index] => array[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (array.IsDefault || other.array.IsDefault)
            {
                return array.IsDefault && other.array.IsDefault;
            }

            return array.AsSpan().SequenceEqual(other.array.AsSpan());
        }

        public override bool Equals(object? obj)
            => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (array.IsDefault)
            {
                return 0;
            }

            var hash = 17;
            foreach (var item in array)
            {
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }

        public IEnumerator<T> GetEnumerator()
            => (array.IsDefault ? Enumerable.Empty<T>() : array).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static implicit operator EquatableArray<T>(ImmutableArray<T> array)
            => new(array);
    }
}
