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
        [Test]
        public void Validate_WhenCircularDependenciesExist_Throws()
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
