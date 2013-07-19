using Stiletto;

namespace CompleteModuleWithInjectTypeProviderParam
{
    public interface IClass
    {

    }

    public class SomeOtherClass : IClass
    {
        [Inject]
        public SomeOtherClass(string str)
        {

        }
    }

    [Module(IsLibrary = true)]
    public class NonProvidedImplementationModule
    {
        [Provides]
        public string ProvideString()
        {
            return "foo";
        }

        [Provides]
        public IClass ProvideClass(SomeOtherClass impl)
        {
            return impl;
        }
    }
}
