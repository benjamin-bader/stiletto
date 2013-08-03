using Stiletto;

namespace DuplicateInjectsTypesFail
{
    public class InjectableClass
    {
        [Inject]
        public string Foo { get; set; }
    }

    [Module(Injects = new[] { typeof(InjectableClass), typeof(InjectableClass) })]
    public class MainModule
    {
        [Provides]
        public string ProvideString()
        {
            return "foo";
        }
    }
}
