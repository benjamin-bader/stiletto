using Xunit;

namespace Stiletto.Tests
{
    public class LazyInjectionTests
    {
        [Fact]
        public void CanMakeProvidedObjectLazy()
        {
            var container = Container.Create(typeof(NonLazyModule));
            var greedy = container.Get<NeedsAnExpensiveObject>();
            Assert.NotNull(greedy.Expensive);
        }

        [Fact]
        public void LazyValue_IsProduced_WhenForced()
        {
            var container = Container.Create(typeof(NonLazyModule));
            var greedy = container.Get<NeedsAnExpensiveObject>();
            Assert.Equal("an expensive web service call", greedy.Expensive.Value);
        }

        [Module(Injects = new[] { typeof(NeedsAnExpensiveObject) })]
        public class NonLazyModule
        {
            [Provides]
            public string SomeExpensiveObject()
            {
                return "an expensive web service call";
            }
        }

        public class NeedsAnExpensiveObject
        {
            private readonly Lazy<string> expensive;

            public Lazy<string> Expensive
            {
                get { return expensive; }
            }

            [Inject]
            public NeedsAnExpensiveObject(Lazy<string> expensive)
            {
                this.expensive = expensive;
            }
        }
    }
}
