using System;
using System.Collections.Generic;
using ExpectBetter;
using NUnit.Framework;

namespace Stiletto.Test
{
    [TestFixture]
    public class RudimentaryInjectionTest
    {
        [Test]
        public void CanGetTheDude()
        {
            var container = Container.Create(typeof(TestNamedModule));
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
            var container = Container.Create(typeof(TestIncludedModules));
            var dude = container.Get<Dude>();
            Expect.The(dude).Not.ToBeNull();
        }

        [Test]
        public void ThereCanBeOnlyOne()
        {
            var container = Container.Create(typeof(TestNamedModule));
            var dude = container.Get<Dude>();
            var otherDude = container.Get<Dude>();
            Expect.The(otherDude).ToBeTheSameAs(dude);
        }

        [Test]
        public void SingletonProviderMethodReturnsSameInstance()
        {
            var container = Container.Create(typeof(TestSingletonProviderModule));
            var injectable = container.Get<SingletonTestInjectable>();

            Expect.The(injectable.One).ToBeTheSameAs(injectable.Another);
        }

        [Test]
        public void NonSingletonProviderMethodReturnsDifferentInstances()
        {
            var injectable = GetWithModules<NonSingletonTestInjectable>(typeof(TestSingletonProviderModule));

            Expect.The(injectable.One).Not.ToBeTheSameAs(injectable.Another);
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

        [Test]
        public void Injectable_Injected_WhenDepenencyNotProvided_GetsJitBinding()
        {
            var injectable = GetWithModules<NeedsA>(new EmptyModule());
            Expect.The(injectable).Not.ToBeNull();
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void DuplicateModuleTypesFail()
        {
            Container.Create(new NonOverridingModule(), new NonOverridingModule());
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void DuplicateProvidedTypesFail()
        {
            Container.Create(new BoolProvidingModule(), new NonOverridingModule());
        }

        [Test]
        public void ModulesCanOverride()
        {
            var container = Container.Create(new OverridingModule(), new NonOverridingModule());
            Expect.The(container.Get<bool>()).ToBeTrue();
        }

        [Test]
        public void ModuleOrderDoesNotMatterForOverriding()
        {
            var c1 = Container.Create(new NonOverridingModule(), new OverridingModule());
            var c2 = Container.Create(new OverridingModule(), new NonOverridingModule());

            var b1 = c1.Get<bool>();
            var b2 = c2.Get<bool>();

            Expect.The(b1).ToEqual(b2);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void BaseClassInstance_InjectingDerivedProperties_FailsWhenGenericIsUsed()
        {
            var container = Container.Create(typeof(NameModule));
            var baseInjectable = new DerivedInjectable("foo") as BaseInjectable;
            container.Inject(baseInjectable);
        }

        [Test]
        public void BaseClassInstance_InjectingDerivedProperties_WorksWhenNonGenericIsUsed()
        {
            var container = Container.Create(typeof(NameModule));
            var baseInjectable = new DerivedInjectable("foo") as BaseInjectable;
            container.Inject(baseInjectable, baseInjectable.GetType());
        }

        [Test]
        public void InjectableDerivedFromNonInjectableIsInjected()
        {
            GetWithModules<DerivedFromNonInjectable>(typeof(BaseNonInjectableModule));
        }

        private class A
        {
            [Inject]
            public A() { }
        }

        private class NeedsA
        {
            [Inject]
            public A A { get; set; }
        }

        [Module(Injects = new[] { typeof(NeedsA) })]
        private class EmptyModule
        {
        }

        private T GetWithModules<T>(params object[] modules)
        {
            return Container.Create(modules).Get<T>();
        }

        [Module(
            Injects = new[] { typeof(SingletonTestInjectable), typeof(NonSingletonTestInjectable) })]
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

        public class SingletonTestInjectable
        {
            [Inject, Named("s")]
            public object One { get; set; }
            [Inject, Named("s")]
            public object Another { get; set; }
        }

        public class NonSingletonTestInjectable
        {
            [Inject, Named("n")]
            public object One { get; set; }
            [Inject, Named("n")]
            public object Another { get; set; }
        }

        [Module(
            Injects = new[] { typeof(Dude) })]
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
            public DateTime FavoriteTimeOfDay { get; set; }

            [Inject]
            public Dude(IList<string> hobbies)
            {
                this.hobbies = hobbies;
            }
        }

        [Module(Injects = new[] { typeof(DerivedInjectable) },
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

        [Module(Injects = new[] { typeof(ThrowsOnNew), typeof(ThrowsOnSet) })]
        public class ThrowableModule
        {
            [Provides]
            public int GetInt()
            {
                return 0;
            }
        }

        public class BaseNonInjectable
        {
        }

        public class DerivedFromNonInjectable : BaseNonInjectable
        {
            private readonly string foo;

            public string Foo
            {
                get { return foo; }
            }

            [Inject]
            public DerivedFromNonInjectable(string foo)
            {
                this.foo = foo;
            }
        }

        [Module(Injects = new[] { typeof(DerivedFromNonInjectable) })]
        public class BaseNonInjectableModule
        {
            [Provides]
            public string ProvideFoo()
            {
                return "foo";
            }
        }

        [Module(IsLibrary = true)]
        public class BoolProvidingModule
        {
            [Provides]
            public bool HereIsABool()
            {
                return false;
            }
        }

        [Module(Injects = new[] { typeof(bool) }, IsLibrary = true)]
        public class NonOverridingModule
        {
            [Provides]
            public bool ProvideBool()
            {
                return false;
            }
        }

        [Module(IsOverride = true, IsLibrary = true)]
        public class OverridingModule
        {
            [Provides]
            public bool ProvideAnotherBool()
            {
                return true;
            }
        }
    }
}
