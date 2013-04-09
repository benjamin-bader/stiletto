using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExpectBetter;
using NUnit.Framework;

namespace Abra.Test
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
            Expect.The(container.Validate).Not.ToThrow<Exception>();
        }

        [Module]
        private class BadModuleOne
        {
            [Provides]
            public string Foo(int i)
            {
                return "";
            }
        }

        [Module]
        private class BadModuleTwo
        {
            [Provides]
            public int Bar(string s)
            {
                return 0;
            }
        }

        [Module]
        private class GoodModuleOne
        {
            [Provides]
            public string Foo(int i)
            {
                return "";
            }
        }

        [Module]
        private class GoodModuleTwo
        {
            [Provides]
            public int Bar()
            {
                return 0;
            }
        }
    }
}
