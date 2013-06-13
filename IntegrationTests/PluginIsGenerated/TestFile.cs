using Stiletto;

namespace PluginIsGenerated
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
}
