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
            public object SomeExpensiveObject()
            {
                return new object();
            }
        }

        private class NeedsAnExpensiveObject
        {
            private readonly Lazy<object> expensive;

            public Lazy<object> Expensive
            {
                get { return expensive; }
            }

            [Inject]
            public NeedsAnExpensiveObject(Lazy<object> expensive)
            {
                this.expensive = expensive;
            }
        }
    }
}
