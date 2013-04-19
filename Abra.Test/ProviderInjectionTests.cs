using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExpectBetter;
using NUnit.Framework;

namespace Abra.Test
{
    [TestFixture]
    public class ProviderInjectionTests
    {
        private NeedsProvider testObj;

        [SetUp]
        public void Setup()
        {
            var container = Container.Create(typeof(TestModule));
            testObj = container.Get<NeedsProvider>();
        }

        [Test]
        public void CanInjectProviderOfT()
        {
            Expect.The(testObj.ObjectProvider).Not.ToBeNull();
        }

        [Test]
        public void InjectedProviderInvokesProviderMethod()
        {
            Expect.The(TestModule.Invocations).ToEqual(0);
            testObj.ObjectProvider.Get();
            testObj.ObjectProvider.Get();
            Expect.The(TestModule.Invocations).ToEqual(2);
        }

        [Module(EntryPoints = new[] { typeof(NeedsProvider) })]
        public class TestModule
        {
            public static int Invocations = 0;

            [Provides]
            public string SomeObject()
            {
                ++Invocations;
                return new string("abcdefg".ToCharArray());
            }
        }

        public class NeedsProvider
        {
            [Inject]
            public IProvider<string> ObjectProvider { get; set; } 
        }
    }
}
