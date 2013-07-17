using Stiletto;

namespace ModulesNeedDefaultConstructors
{
    public class Foo
    {
        [Inject]
        public Bar Bar { get; set; }
    }

    public class Bar
    {
        [Inject]
        public Foo Foo { get; set; }
    }

    public class Foobar
    {
        [Inject]
        public Foobar(Foo foo, Bar bar)
        {
            
        }
    }

    [Module(Injects = new[] { typeof(Foobar) })]
    public class MainModule
    {
        public MainModule(int someParam)
        {

        }
    }
}
