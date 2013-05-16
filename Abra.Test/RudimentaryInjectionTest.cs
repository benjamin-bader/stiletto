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
            var entryPoint = GetWithModules<NonSingletonTestEntryPoint>(typeof (TestSingletonProviderModule));

            Expect.The(entryPoint.One).Not.ToBeTheSameAs(entryPoint.Another);
        }

        [Test]
        public void ModulesCanBeInstances()
        {
            var guy = GetWithModules<Dude>(new TestNamedModule("going outside", "dancing"));
            var listOfHobbies = guy.Hobbies;

            Expect.The(listOfHobbies).ToContain("dancing");
            Expect.The(listOfHobbies).Not.ToContain("dependency injection");
        }

        [Test]
        public void BaseClassGetsInjectedToo()
        {
            var derived = GetWithModules<DerivedInjectable>(new NameModule());
            Expect.The(derived.TheDude).Not.ToBeNull();
            Expect.The(derived.Name).ToEqual("Joe");
        }

        [Test, ExpectedException(typeof(PlatformNotSupportedException))]
        public void ConstructorExceptionsPropagate()
        {
            GetWithModules<ThrowsOnNew>(new ThrowableModule());
        }

        [Test, ExpectedException(typeof(PlatformNotSupportedException))]
        public void PropertySetterExceptionsPropagate()
        {
            GetWithModules<ThrowsOnSet>(new ThrowableModule());
        }

        private T GetWithModules<T>(params object[] modules)
        {
            return Container.Create(modules).Get<T>();
        }

        [Module(
            EntryPoints = new[] { typeof(object) })]
        public class TestModule
        {
            [Provides]
            public object ProvideFoo()
            {
                return Foo;
            }
        }

        [Module(
            EntryPoints = new[] { typeof(SingletonTestEntryPoint), typeof(NonSingletonTestEntryPoint)})]
        public class TestSingletonProviderModule
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

        public class SingletonTestEntryPoint
        {
            [Inject, Named("s")] public object One { get; set; }
            [Inject, Named("s")] public object Another { get; set; }
        }

        public class NonSingletonTestEntryPoint
        {
            [Inject, Named("n")] public object One { get; set; }
            [Inject, Named("n")] public object Another { get; set; }
        }

        [Module(
            EntryPoints = new[] { typeof(Dude) })]
        public class TestNamedModule
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

        [Module]
        public class ArrayModule
        {
            [Provides]
            public object[,] ProvidesStringArray()
            {
                return new object[0,0];
            }
        }

        [Module(IncludedModules = new[] { typeof(TestNamedModule) })]
        public class TestIncludedModules
        {
            // This space intentionally left blank
        }

        [Singleton]
        public class Dude
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

        [Module(EntryPoints = new[] { typeof(DerivedInjectable) },
            IncludedModules = new[] { typeof(TestNamedModule) })]
        public class NameModule
        {
            [Provides]
            public string GetName()
            {
                return "Joe";
            }
        }

        public class BaseInjectable
        {
            [Inject]
            public Dude TheDude { get; set; }
        }

        public class DerivedInjectable : BaseInjectable
        {
            private readonly string name;

            public string Name
            {
                get { return name; }
            }

            [Inject]
            public DerivedInjectable(string name)
            {
                this.name = name;
            }
        }

        public class ThrowsOnNew
        {
            [Inject]
            public ThrowsOnNew(int arg)
            {
                throw new PlatformNotSupportedException();
            }
        }

        public class ThrowsOnSet
        {
            private int n;

            [Inject]
            public int Dependency
            {
                get { return n; }
                set { n = value; throw new PlatformNotSupportedException(); }
            }
        }

        [Module(EntryPoints = new[] { typeof(ThrowsOnNew), typeof(ThrowsOnSet) })]
        public class ThrowableModule
        {
            [Provides]
            public int GetInt()
            {
                return 0;
            }
        }
    }
}
