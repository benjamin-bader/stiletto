using System;
using System.Collections.Generic;
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
        public void CanGetTheDude()
        {
            var container = Container.Create(typeof (TestNamedModule));
            var dude = container.Get<Dude>();
            Expect.The(dude).Not.ToBeNull();

            var birthday = dude.Birthday;
            Expect.The(birthday).ToEqual(new DateTime(1982, 12, 3));

            var listOfHobbies = dude.Hobbies;
            Expect.The(listOfHobbies).ToContain("dependency injection");
        }

        [Test]
        public void CanGetTheDudeFromAnIncludedModule()
        {
            var container = Container.Create(typeof (TestIncludedModules));
            var dude = container.Get<Dude>();
            Expect.The(dude).Not.ToBeNull();
        }

        [Test]
        public void ThereCanBeOnlyOne()
        {
            var container = Container.Create(typeof (TestNamedModule));
            var dude = container.Get<Dude>();
            var otherDude = container.Get<Dude>();
            Expect.The(otherDude).ToBeTheSameAs(dude);
        }

        [Test]
        public void SingletonProviderMethodReturnsSameInstance()
        {
            var container = Container.Create(typeof (TestSingletonProviderModule));
            var entryPoint = container.Get<SingletonTestEntryPoint>();

            Expect.The(entryPoint.One).ToBeTheSameAs(entryPoint.Another);
        }

        [Test]
        public void NonSingletonProviderMethodReturnsDifferentInstances()
        {
            var container = Container.Create(typeof(TestSingletonProviderModule));
            var entryPoint = container.Get<NonSingletonTestEntryPoint>();

            Expect.The(entryPoint.One).Not.ToBeTheSameAs(entryPoint.Another);
        }

        [Test]
        public void ModulesCanBeInstances()
        {
            var container = Container.Create(new TestNamedModule("going outside", "dancing"));
            var guy = container.Get<Dude>();
            var listOfHobbies = guy.Hobbies;

            Expect.The(listOfHobbies).ToContain("dancing");
            Expect.The(listOfHobbies).Not.ToContain("dependency injection");
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
            EntryPoints = new[] { typeof(SingletonTestEntryPoint), typeof(NonSingletonTestEntryPoint)})]
        private class TestSingletonProviderModule
        {
            [Provides, Named("n")]
            public object NewEveryTime()
            {
                return new object();
            }

            [Provides, Named("s"), Singleton]
            public object Singleton()
            {
                return new object();
            }
        }

        private class SingletonTestEntryPoint
        {
            [Inject, Named("s")] public object One { get; set; }
            [Inject, Named("s")] public object Another { get; set; }
        }

        private class NonSingletonTestEntryPoint
        {
            [Inject, Named("n")] public object One { get; set; }
            [Inject, Named("n")] public object Another { get; set; }
        }

        [Module(
            EntryPoints = new[] { typeof(Dude) })]
        private class TestNamedModule
        {
            private readonly IList<string> hobbies;

            public TestNamedModule()
                : this("dependency injection")
            {
            }

            public TestNamedModule(params string[] hobbies)
            {
                this.hobbies = hobbies;
            }

            [Provides, Named("bar")]
            public DateTime GetBar()
            {
                return new DateTime(1982, 12, 3);
            }

            [Provides]
            public DateTime GetSomeOtherDate()
            {
                return DateTime.Now;
            }

            [Provides]
            public IList<string> Activities()
            {
                return new List<string>(hobbies);
            }
        }

        [Module(IncludedModules = new[] { typeof(TestNamedModule) })]
        private class TestIncludedModules
        {
            // This space intentionally left blank
        }

        [Singleton]
        private class Dude
        {
            private readonly IList<string> hobbies;

            public IList<string> Hobbies
            {
                get { return hobbies; }
            }

            [Inject, Named("bar")]
            public DateTime Birthday { get; set; }


            [Inject]
            public Dude(IList<string> hobbies)
            {
                this.hobbies = hobbies;
            }
        }
    }
}
