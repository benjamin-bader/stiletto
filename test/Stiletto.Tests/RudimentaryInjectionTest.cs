using Xunit;

namespace Stiletto.Tests
{
    public class RudimentaryInjectionTest
    {
        [Fact]
        public void CanGetTheDude()
        {
            var container = Container.Create(typeof(TestNamedModule));
            var dude = container.Get<Dude>();
            Assert.NotNull(dude);

            Assert.Equal(new DateTime(1982, 12, 3), dude.Birthday);
            Assert.Contains("dependency injection", dude.Hobbies);
        }

        [Fact]
        public void CanGetTheDudeFromAnIncludedModule()
        {
            var container = Container.Create(typeof(TestIncludedModules));
            var dude = container.Get<Dude>();
            Assert.NotNull(dude);
        }

        [Fact]
        public void ThereCanBeOnlyOne()
        {
            var container = Container.Create(typeof(TestNamedModule));
            var dude = container.Get<Dude>();
            var otherDude = container.Get<Dude>();
            Assert.Same(dude, otherDude);
        }

        [Fact]
        public void SingletonProviderMethodReturnsSameInstance()
        {
            var container = Container.Create(typeof(TestSingletonProviderModule));
            var injectable = container.Get<SingletonTestInjectable>();

            Assert.Same(injectable.One, injectable.Another);
        }

        [Fact]
        public void NonSingletonProviderMethodReturnsDifferentInstances()
        {
            var injectable = GetWithModules<NonSingletonTestInjectable>(typeof(TestSingletonProviderModule));

            Assert.NotSame(injectable.One, injectable.Another);
        }

        [Fact]
        public void ModulesCanBeInstances()
        {
            var guy = GetWithModules<Dude>(new TestNamedModule("going outside", "dancing"));
            var listOfHobbies = guy.Hobbies;

            Assert.Contains("dancing", listOfHobbies);
            Assert.DoesNotContain("dependency injection", listOfHobbies);
        }

        [Fact]
        public void BaseClassGetsInjectedToo()
        {
            var derived = GetWithModules<DerivedInjectable>(new NameModule());
            Assert.NotNull(derived.TheDude);
            Assert.Equal("Joe", derived.Name);
        }

        [Fact]
        public void ConstructorExceptionsPropagate()
        {
            Assert.Throws<PlatformNotSupportedException>(
                () => GetWithModules<ThrowsOnNew>(new ThrowableModule()));
        }

        [Fact]
        public void PropertySetterExceptionsPropagate()
        {
            Assert.Throws<PlatformNotSupportedException>(
                () => GetWithModules<ThrowsOnSet>(new ThrowableModule()));
        }

        [Fact]
        public void Injectable_Injected_WhenDepenencyNotProvided_GetsJitBinding()
        {
            var injectable = GetWithModules<NeedsA>(new EmptyModule());
            Assert.NotNull(injectable);
        }

        [Fact]
        public void DuplicateModuleTypesFail()
        {
            Assert.Throws<ArgumentException>(
                () => Container.Create(new NonOverridingModule(), new NonOverridingModule()));
        }

        [Fact]
        public void DuplicateProvidedTypesFail()
        {
            Assert.Throws<ArgumentException>(
                () => Container.Create(new BoolProvidingModule(), new NonOverridingModule()));
        }

        [Fact]
        public void ModulesCanOverride()
        {
            var container = Container.Create(new OverridingModule(), new NonOverridingModule());
            Assert.True(container.Get<bool>());
        }

        [Fact]
        public void ModuleOrderDoesNotMatterForOverriding()
        {
            var c1 = Container.Create(new NonOverridingModule(), new OverridingModule());
            var c2 = Container.Create(new OverridingModule(), new NonOverridingModule());

            Assert.Equal(c1.Get<bool>(), c2.Get<bool>());
        }

        [Fact]
        public void BaseClassInstance_InjectingDerivedProperties_FailsWhenGenericIsUsed()
        {
            var container = Container.Create(typeof(NameModule));
            var baseInjectable = new DerivedInjectable("foo") as BaseInjectable;
            Assert.Throws<ArgumentException>(() => container.Inject(baseInjectable));
        }

        [Fact]
        public void BaseClassInstance_InjectingDerivedProperties_WorksWhenNonGenericIsUsed()
        {
            var container = Container.Create(typeof(NameModule));
            var baseInjectable = new DerivedInjectable("foo") as BaseInjectable;
            container.Inject(baseInjectable, baseInjectable.GetType());
        }

        [Fact]
        public void InjectableDerivedFromNonInjectableIsInjected()
        {
            GetWithModules<DerivedFromNonInjectable>(typeof(BaseNonInjectableModule));
        }

        [Fact]
        public void InjectableDerivedFromNonInjectable_WithSuper_IsInjected()
        {
            GetWithModules<DerivedWithSuper>(typeof(TypeProvidingModule));
        }

        private T GetWithModules<T>(params object[] modules)
        {
            return Container.Create(modules).Get<T>();
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

        public class BaseNonInjectibleWithSuperRequired
        {
            public Type Type { get; private set; }

            public BaseNonInjectibleWithSuperRequired(Type t)
            {
                Type = t;
            }
        }

        public class DerivedWithSuper : BaseNonInjectibleWithSuperRequired
        {
            [Inject]
            public DerivedWithSuper(Type t)
                : base(t)
            { }
        }

        [Module(Injects = new[] { typeof(DerivedWithSuper) })]
        public class TypeProvidingModule
        {
            [Provides]
            public Type HaveAType()
            {
                return typeof(TypeProvidingModule);
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
