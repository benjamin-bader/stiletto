using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpectBetter;
using NUnit.Framework;

namespace Abra.Test
{
    public abstract class KeyTestsBase
    {
        protected abstract string GetKey<T>(string name = null);

        protected abstract string GetMemberKey<T>();

        protected abstract string GetProviderKey(string key);

        protected abstract string GetLazyKey(string key);

        protected abstract bool IsNamed(string key);

        [Test]
        public void SimpleTypes_EqualReflectionFullName()
        {
            var stringKey = GetKey<string>();
            var intKey = GetKey<int>();
            var iListKey = GetKey<System.Collections.IList>();

            Expect.The(stringKey).ToEqual("System.String");
            Expect.The(intKey).ToEqual("System.Int32");
            Expect.The(iListKey).ToEqual("System.Collections.IList");
        }

        [Test]
        public void MemberKeys_OfSimpleTypes_EqualPrefixPlusReflectionFullName()
        {
            var stringKey = GetMemberKey<string>();
            var intKey = GetMemberKey<int>();
            var iListKey = GetMemberKey<System.Collections.IList>();

            Expect.The(stringKey).ToEqual("members/System.String");
            Expect.The(intKey).ToEqual("members/System.Int32");
            Expect.The(iListKey).ToEqual("members/System.Collections.IList");
        }

        [Test]
        public void NamedKeys_OfSimpleTypes_EqualNamePlusReflectionFullName()
        {
            var stringKey = GetKey<string>("foo");
            var intKey = GetKey<int>("bar");
            var iListKey = GetKey<System.Collections.IList>("baz");

            Expect.The(stringKey).ToEqual("@foo/System.String");
            Expect.The(intKey).ToEqual("@bar/System.Int32");
            Expect.The(iListKey).ToEqual("@baz/System.Collections.IList");
        }

        [Test]
        public void Arrays_EqualReflectionFullNamePlusRankedSuffix()
        {
            var intArrayKey = GetKey<int[]>();
            var multiDimensionalArrayKey = GetKey<object[,,]>();
            var jaggedArrayKey = GetKey<decimal[,][]>();

            Expect.The(intArrayKey).ToEqual("System.Int32[]");
            Expect.The(multiDimensionalArrayKey).ToEqual("System.Object[,,]");
            Expect.The(jaggedArrayKey).ToEqual("System.Decimal[][,]");
            // Note the last case is not an error, the brackets really are transposed.
            // The C# type declaration appears ambiguous, but it really is a 2D array
            // of int arrays.
        }

        [Test]
        public void Generics_LookLikeCSharpGenerics()
        {
            var intListKey = GetKey<List<int>>();
            var dictKey = GetKey<IDictionary<string, object>>();

            Expect.The(intListKey).ToEqual("System.Collections.Generic.List`1<System.Int32>");
            Expect.The(dictKey).ToEqual("System.Collections.Generic.IDictionary`2<System.String,System.Object>");
        }

        [Test]
        public void ProviderKeys_CanHaveProvidedTypeExtracted()
        {
            var intProviderKey = GetKey<IProvider<int>>();
            var providedTypeKey = GetProviderKey(intProviderKey);
            Expect.The(providedTypeKey).Not.ToBeNull();
            Expect.The(providedTypeKey).ToEqual("System.Int32");
        }

        [Test]
        public void LazyKeys_CanHaveLazyTypeExtracted()
        {
            var lazyKey = GetKey<Lazy<object>>();
            var providedTypeKey = GetLazyKey(lazyKey);
            Expect.The(providedTypeKey).ToEqual("System.Object");
        }

        [Test]
        public void NamedKeys_CanBeDetected()
        {
            var namedKey = GetKey<object>("foo");
            var anonymousKey = GetKey<object>();

            Expect.The(namedKey).Not.ToEqual(anonymousKey);
            Expect.The(IsNamed(namedKey)).ToBeTrue();
            Expect.The(IsNamed(anonymousKey)).ToBeFalse();
        }
    }
}
