using NUnit.Framework;

namespace Stiletto.Test
{
    [TestFixture]
    public class KeyTests : KeyTestsBase
    {
        protected override string GetKey<T>(string name = null)
        {
            return Key.Get(typeof(T), name);
        }

        protected override string GetMemberKey<T>()
        {
            return Key.GetMemberKey<T>();
        }

        protected override string GetProviderKey(string key)
        {
            return Key.GetProviderKey(key);
        }

        protected override string GetLazyKey(string key)
        {
            return Key.GetLazyKey(key);
        }

        protected override bool IsNamed(string key)
        {
            return Key.IsNamed(key);
        }
    }
}
