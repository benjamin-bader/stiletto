using Xunit;

namespace Stiletto.Tests
{
    public class SetInjectionTests
    {
        [Fact]
        public void SetBindings_ContributeToInjectedSet()
        {
            var needsSet = Container.Create(new NeedsSetModule()).Get<NeedsSet>();
            Assert.Contains("foo", needsSet.Strings);
            Assert.Contains("bar", needsSet.Strings);
            Assert.Equal(2, needsSet.Strings.Count);
        }

        [Fact]
        public void NamedSetsWork()
        {
            var names = Container.Create(new HasNamedSetsModule()).Get<HasNamedSets>();
            Assert.Equal(2, names.BoyNames.Count);
            Assert.Equal(2, names.GirlNames.Count);
            Assert.Contains("Billie", names.BoyNames);
            Assert.Contains("Cory", names.BoyNames);
            Assert.Contains("Pat", names.GirlNames);
            Assert.Contains("Devon", names.GirlNames);
        }

        [Fact]
        public void SetElementsCanBeSingletons()
        {
            var sets = Container.Create(typeof(SingletonSetElementModule)).Get<HasSingletonSetElement>();
            Assert.True(sets.SetOne.Overlaps(sets.SetTwo));
        }

        [Fact]
        public void SetElements_InLibraryModules_AreNotSubjectToOrphanAnalysis()
        {
            var container = Container.Create(typeof(ModuleWithLibraryOrphanStringSet));
            container.Validate();
        }

        [Fact]
        public void SetElements_InNonLibraryModules_AreSubjectToOrphanAnalysis()
        {
            Assert.Throws<InvalidOperationException>(
                () => Container.Create(typeof(ModuleWithNonLibraryOrphanStringSet)).Validate());
        }

        public class NeedsSet
        {
            [Inject]
            public ISet<string> Strings { get; set; }
        }

        [Module(Injects = new[] { typeof(NeedsSet) })]
        public class NeedsSetModule
        {
            [Provides(ProvidesType.Set)]
            public string ProvideFoo()
            {
                return "foo";
            }

            [Provides(ProvidesType.Set)]
            public string ProvidesBar()
            {
                return "bar";
            }
        }

        public class HasNamedSets
        {
            [Inject, Named("boy-names")]
            public ISet<string> BoyNames { get; set; }

            [Inject, Named("girl-names")]
            public ISet<string> GirlNames { get; set; }
        }

        [Module(Injects = new[] { typeof(HasNamedSets) })]
        public class HasNamedSetsModule
        {
            [Provides(ProvidesType.Set), Named("boy-names")]
            public string ProvideNameOne()
            {
                return "Cory";
            }

            [Provides(ProvidesType.Set), Named("girl-names")]
            public string ProvideNameTwo()
            {
                return "Devon";
            }

            [Provides(ProvidesType.Set), Named("boy-names")]
            public string ProvideNameThree()
            {
                return "Billie";
            }

            [Provides(ProvidesType.Set), Named("girl-names")]
            public string ProvideNameFour()
            {
                return "Pat";
            }
        }

        public class HasSingletonSetElement
        {
            public ISet<object> SetOne { get; private set; }
            public ISet<object> SetTwo { get; private set; }

            [Inject]
            public HasSingletonSetElement(ISet<object> setOne, ISet<object> setTwo)
            {
                SetOne = setOne;
                SetTwo = setTwo;
            }
        }

        [Module(Injects = new[] { typeof(HasSingletonSetElement) })]
        public class SingletonSetElementModule
        {
            [Provides(ProvidesType.Set), Singleton]
            public object ProvideSingletonObject()
            {
                return new object();
            }

            [Provides(ProvidesType.Set)]
            public object ProvideTransientObject()
            {
                return new object();
            }
        }

        public class NeedsNothing
        {
        }

        [Module(IsLibrary = true)]
        public class StringSetLibraryModule
        {
            [Provides(ProvidesType.Set)]
            public string ProvideString()
            {
                return "foo";
            }
        }

        [Module(Injects = new[] { typeof(NeedsNothing) },
            IncludedModules = new[] { typeof(StringSetLibraryModule) })]
        public class ModuleWithLibraryOrphanStringSet
        {
        }

        [Module]
        public class StringSetNonLibraryModule
        {
            [Provides(ProvidesType.Set)]
            public string ProvideAnotherString()
            {
                return "bar";
            }
        }

        [Module(Injects = new[] { typeof(NeedsNothing) },
            IncludedModules = new[] { typeof(StringSetLibraryModule), typeof(StringSetNonLibraryModule) })]
        public class ModuleWithNonLibraryOrphanStringSet
        {
        }
    }
}
