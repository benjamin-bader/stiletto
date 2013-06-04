using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExpectBetter;
using NUnit.Framework;

namespace Stiletto.Test
{
    [TestFixture]
    public class ValidatorTests
    {
        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void Validate_WhenDependencyIsUnsatisfied_Throws()
        {
            Container.Create(typeof(NeedsSomethingMore)).Validate();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void Validate_WhenCircularDependenciesExist_Throws()
        {
            Container.Create(typeof(CaptainPlanet)).Validate();
        }

        [Test]
        public void Validate_WhenCircularDependenciesExistAcrossModules_Throws()
        {
            var container = Container.Create(typeof (BadModuleOne), typeof (BadModuleTwo));
            Expect.The(container.Validate).ToThrow<InvalidOperationException>();
        }

        [Test]
        public void Validate_WhenNoCyclesExist_DoesNotThrow()
        {
            var container = Container.Create(typeof(GoodModuleOne), typeof(GoodModuleTwo));
            container.Validate();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void Validate_WhenProviderMethodsAreUnused_Throws()
        {
            Container.Create(typeof(UnusedProvidesModule)).Validate();
        }

        [Module(EntryPoints = new[] { typeof(Dep)})]
        public class NeedsSomethingMore
        {
            public class Dep
            {
                [Inject]
                public string Str { get; set; }

                [Inject]
                public object Bar { get; set; }
            }

            [Provides]
            public string GetString()
            {
                return "foo";
            }
        }

        public class Earth
        {
            [Inject]
            public Wind Wind { get; set; }
        }

        public class Wind
        {
            [Inject]
            public Fire Fire { get; set; }
        }

        public class Fire
        {
            [Inject]
            public Water Water { get; set; }
        }

        public class Water
        {
            [Inject]
            public Heart Heart { get; set; }
        }

        public class Heart
        {
            [Inject]
            public Earth Earth { get; set; }
        }

        [Module(EntryPoints = new[] { typeof(Earth) })]
        public class CaptainPlanet
        {
        }

        public class UnusedProvidesEntryPoint
        {
            [Inject]
            public string Foo { get; set; }
        }

        [Module(EntryPoints = new[] { typeof(UnusedProvidesEntryPoint) })]
        public class UnusedProvidesModule
        {
            [Provides]
            public string ProvideString()
            {
                return "foo";
            }

            [Provides]
            public int ProvideInt()
            {
                return -1;
            }
        }

        [Module(IsComplete = false)]
        public class BadModuleOne
        {
            [Provides]
            public string Foo(int i)
            {
                return "";
            }
        }

        [Module(IsComplete = false)]
        public class BadModuleTwo
        {
            [Provides]
            public int Bar(string s)
            {
                return 0;
            }
        }

        [Module(IsComplete = false, IsLibrary = true)]
        public class GoodModuleOne
        {
            [Provides]
            public string Foo(int i)
            {
                return "";
            }
        }

        [Module(IsLibrary = true)]
        public class GoodModuleTwo
        {
            [Provides]
            public int Bar()
            {
                return 0;
            }
        }
    }
}
