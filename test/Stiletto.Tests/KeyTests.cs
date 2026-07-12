using Xunit;

namespace Stiletto.Tests
{
    public class KeyTests
    {
        [Fact]
        public void GetProviderKey_ExtractsProvidedType_ForAProviderKey()
        {
            var providerKey = Key.Get(typeof(IProvider<int>))!;

            Assert.Equal(Key.Get(typeof(int)), Key.GetProviderKey(providerKey));
        }

        [Fact]
        public void GetLazyKey_ExtractsLazyType_ForALazyKey()
        {
            var lazyKey = Key.Get(typeof(Lazy<int>))!;

            Assert.Equal(Key.Get(typeof(int)), Key.GetLazyKey(lazyKey));
        }

        [Fact]
        public void GetProviderKey_ReturnsNull_WhenProviderIsOnlyANestedTypeArgument()
        {
            // List<IProvider<int>> merely *contains* the provider prefix as a nested
            // generic argument; its key does not *start* with it, so it is not a
            // provider binding.
            var key = Key.Get(typeof(List<IProvider<int>>))!;

            Assert.Null(Key.GetProviderKey(key));
        }

        [Fact]
        public void GetLazyKey_ReturnsNull_WhenLazyIsOnlyANestedTypeArgument()
        {
            var key = Key.Get(typeof(List<Lazy<int>>))!;

            Assert.Null(Key.GetLazyKey(key));
        }

        [Fact]
        public void SimpleTypes_EqualReflectionFullName()
        {
            Assert.Equal("System.String", Key.Get(typeof(string)));
            Assert.Equal("System.Int32", Key.Get(typeof(int)));
            Assert.Equal("System.Collections.IList", Key.Get(typeof(System.Collections.IList)));
        }

        [Fact]
        public void MemberKeys_OfSimpleTypes_EqualPrefixPlusReflectionFullName()
        {
            Assert.Equal("members/System.String", Key.GetMemberKey<string>());
            Assert.Equal("members/System.Int32", Key.GetMemberKey<int>());
            Assert.Equal("members/System.Collections.IList", Key.GetMemberKey<System.Collections.IList>());
        }

        [Fact]
        public void NamedKeys_OfSimpleTypes_EqualNamePlusReflectionFullName()
        {
            Assert.Equal("@foo/System.String", Key.Get(typeof(string), "foo"));
            Assert.Equal("@bar/System.Int32", Key.Get(typeof(int), "bar"));
            Assert.Equal("@baz/System.Collections.IList", Key.Get(typeof(System.Collections.IList), "baz"));
        }

        [Fact]
        public void Arrays_EqualReflectionFullNamePlusRankedSuffix()
        {
            Assert.Equal("System.Int32[]", Key.Get(typeof(int[])));
            Assert.Equal("System.Object[,,]", Key.Get(typeof(object[,,])));
            // Note the brackets really are transposed: the C# declaration decimal[,][]
            // is a 2D array of decimal arrays, not the other way around.
            Assert.Equal("System.Decimal[][,]", Key.Get(typeof(decimal[,][])));
        }

        [Fact]
        public void Generics_LookLikeCSharpGenerics()
        {
            Assert.Equal(
                "System.Collections.Generic.List`1<System.Int32>",
                Key.Get(typeof(List<int>)));
            Assert.Equal(
                "System.Collections.Generic.IDictionary`2<System.String,System.Object>",
                Key.Get(typeof(IDictionary<string, object>)));
        }

        [Fact]
        public void ProviderKeys_CanHaveProvidedTypeExtracted()
        {
            var providedTypeKey = Key.GetProviderKey(Key.Get(typeof(IProvider<int>))!);
            Assert.NotNull(providedTypeKey);
            Assert.Equal("System.Int32", providedTypeKey);
        }

        [Fact]
        public void LazyKeys_CanHaveLazyTypeExtracted()
        {
            var lazyTypeKey = Key.GetLazyKey(Key.Get(typeof(Lazy<object>))!);
            Assert.Equal("System.Object", lazyTypeKey);
        }

        [Fact]
        public void NamedKeys_CanBeDetected()
        {
            var namedKey = Key.Get(typeof(object), "foo")!;
            var anonymousKey = Key.Get(typeof(object))!;

            Assert.NotEqual(anonymousKey, namedKey);
            Assert.True(Key.IsNamed(namedKey));
            Assert.False(Key.IsNamed(anonymousKey));
        }
    }
}
