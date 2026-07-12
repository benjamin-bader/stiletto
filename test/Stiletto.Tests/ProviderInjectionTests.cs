using Xunit;

namespace Stiletto.Tests
{
    public class ProviderInjectionTests
    {
        private readonly NeedsProvider testObj;
        private readonly TestModule module;

        public ProviderInjectionTests()
        {
            module = new TestModule();
            var container = Container.Create(module);
            testObj = container.Get<NeedsProvider>();
        }

        [Fact]
        public void CanInjectProviderOfT()
        {
            Assert.NotNull(testObj.ObjectProvider);
        }

        [Fact]
        public void InjectedProviderInvokesProviderMethod()
        {
            Assert.Equal(0, module.Invocations);
            testObj.ObjectProvider.Get();
            testObj.ObjectProvider.Get();
            Assert.Equal(2, module.Invocations);
        }

        [Module(Injects = new[] { typeof(NeedsProvider) })]
        public class TestModule
        {
            public int Invocations = 0;

            [Provides]
            public string SomeObject()
            {
                ++Invocations;
                return new string("abcdefg".ToCharArray());
            }
        }

        public class NeedsProvider
        {
            [Inject]
            public IProvider<string> ObjectProvider { get; set; }
        }
    }
}
