using CanInjectCrossAssemblies.Common;
using Stiletto;

namespace CanInjectCrossAssemblies.Main
{
    [Module(Injects = new[] { typeof(Injectable) })]
    public class MainModule
    {
        [Provides]
        public string ProvideString()
        {
            return "";
        }

        [Provides]
        public int ProvideInt()
        {
            return 0;
        }
    }
}
