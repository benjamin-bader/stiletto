using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExpectBetter;
using NUnit.Framework;

namespace Stiletto.Test
{
    [TestFixture]
    public class ProviderInjectionTests
    {
        private NeedsProvider testObj;
        private TestModule module;

        [SetUp]
        public void Setup()
        {
            module = new TestModule();
            var container = Container.Create(module);
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
            Expect.The(module.Invocations).ToEqual(0);
            testObj.ObjectProvider.Get();
            testObj.ObjectProvider.Get();
            Expect.The(module.Invocations).ToEqual(2);
        }

        [Module(Injects = new[] { typeof(NeedsProvider) })]
        public class TestModule
        {
            public int Invocations = 0;

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
