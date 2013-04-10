using System;

using ExpectBetter;
using NUnit.Framework;

namespace Abra.Test
{
    [TestFixture]
    public class LazyInjectionTests
    {
        [Test]
        public void CanMakeProvidedObjectLazy()
        {
            var container = Container.Create(typeof (NonLazyModule));
            var greedy = container.Get<NeedsAnExpensiveObject>();
            Expect.The(greedy.Expensive).Not.ToBeNull();
        }

        [Module(EntryPoints = new[] { typeof(NeedsAnExpensiveObject) })]
        private class NonLazyModule
        {
            [Provides]
            public string SomeExpensiveObject()
            {
                return "an expensive web service call";
            }
        }

        private class NeedsAnExpensiveObject
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
