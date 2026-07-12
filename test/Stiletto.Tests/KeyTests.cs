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
    }
}
