using Stiletto;

namespace ContainerCreateCallsRewritten
{
    public class InjectableClass
    {
        [Inject]
        public string Foo { get; set; }
    }

    [Module(EntryPoints = new[] { typeof(InjectableClass) })]
    public class MainModule
    {
        [Provides]
        public string ProvideString()
        {
            return "foo";
        }
    }

    public class Program
    {
        public static void Main()
        {
            var container = Container.Create(typeof(MainModule));
            container.Get<InjectableClass>();
        }
    }
}
