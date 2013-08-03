using Stiletto;

namespace DuplicateInjectsFromIncludedModuleFails
{
    public class InjectableClass
    {
        [Inject]
        public string Foo { get; set; }
    }

    [Module(
        Injects = new[] { typeof(InjectableClass) },
        IncludedModules = new[] { typeof(IncludedModule) })]
    public class MainModule
    {
        [Provides]
        public string ProvideString()
        {
            return "foo";
        }
    }

    [Module(
        Injects = new[] {typeof (InjectableClass)},
        IsComplete = false)]
    public class IncludedModule
    {
        
    }
}
