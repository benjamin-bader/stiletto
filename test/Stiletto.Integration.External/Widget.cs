using Stiletto;

namespace Stiletto.Integration.External
{
    // An injectable whose [Inject] constructor pulls dependencies that only a
    // module in *another* assembly provides. The generator compiles a binding for
    // this type into THIS assembly; the module that supplies string/int lives in
    // Stiletto.Integration.Tests. Resolving it therefore only works if both
    // assemblies' compiled loaders are aggregated at runtime.
    public class Widget
    {
        public readonly string Name;
        public readonly int Count;

        [Inject]
        public Widget(string name, int count)
        {
            Name = name;
            Count = count;
        }
    }
}
