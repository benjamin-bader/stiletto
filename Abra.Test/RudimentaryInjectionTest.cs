using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExpectBetter;
using NUnit.Framework;

namespace Abra.Test
{
    [TestFixture]
    public class RudimentaryInjectionTest
    {
        private static readonly object Foo = new object();

        [Test]
        public void CanGetFoo()
        {
            var container = Container.Create(typeof (TestModule));
            var maybeFoo = container.Get<object>();
            Expect.The(maybeFoo).ToBeTheSameAs(Foo);
        }

        [Test]
        public void CanGetBen()
        {
            var container = Container.Create(typeof (TestNamedModule));
            var ben = container.Get<Ben>();
            Expect.The(ben).Not.ToBeNull();
        }

        [Module(
            EntryPoints = new[] { typeof(object) })]
        private class TestModule
        {
            [Provides]
            public object ProvideFoo()
            {
                return Foo;
            }
        }

        [Module(
            EntryPoints = new[] { typeof(Ben) })]
        private class TestNamedModule
        {
            [Provides, Named("bar")]
            public DateTime GetBar()
            {
                return new DateTime(1982, 12, 3);
            }
        }

        private class Ben
        {
            private readonly DateTime birthday;

            [Inject]
            public Ben([Named("bar")] DateTime birthday)
            {
                this.birthday = birthday;
            }
        }
    }
}
