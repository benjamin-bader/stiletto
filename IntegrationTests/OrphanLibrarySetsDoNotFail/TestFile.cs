using Stiletto;

namespace OrphanLibrarySetsDoNotFail
{
    public class InjectableClass
    {
        [Inject]
        public object Obj { get; set; }
    }

    [Module(Injects = new[] { typeof(InjectableClass) },
        IncludedModules = new[] { typeof(SetLibraryModule) })]
    public class MainSetLibraryModule
    {
        [Provides]
        public object ProvideObject()
        {
            return new object();
        }
    }

    [Module(IsLibrary = true)]
    public class SetLibraryModule
    {
        [Provides(ProvidesType.Set)]
        public string ProvideFoo()
        {
            return "";
        }
    }
}
