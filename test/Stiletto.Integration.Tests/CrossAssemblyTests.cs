using System.Linq;
using Stiletto;
using Stiletto.Integration.External;
using Xunit;

namespace Stiletto.Integration.Tests
{
    /// <summary>
    /// Ports the old <c>CanInjectCrossAssemblies</c> integration test to the source
    /// generator world. The injectable (<see cref="Widget"/>) is compiled — with its
    /// own binding and self-registering loader — into a *different* assembly than the
    /// module that provides its dependencies. Resolving it exercises the runtime's
    /// aggregation of per-assembly <c>CompiledLoader</c>s.
    /// </summary>
    public class CrossAssemblyTests
    {
        [Fact]
        public void GeneratorRanInTheExternalAssembly_AndEmittedItsBinding()
        {
            var externalAssembly = typeof(Widget).Assembly;

            // The generator ran during the external assembly's own build, so its
            // compiled binding and aggregated loader live there — not in this project.
            Assert.NotNull(externalAssembly.GetType("Stiletto.Integration.External.Widget_CompiledBinding"));
            Assert.NotNull(externalAssembly.GetType("Stiletto.Generated.CompiledLoader"));
        }

        [Fact]
        public void CompiledBindingFromAnotherAssembly_ResolvesThroughAggregatedLoaders()
        {
            // Touching a type from the external assembly forces it to load, which fires
            // its [ModuleInitializer] and registers its CompiledLoader.
            var externalAssembly = typeof(Widget).Assembly;

            Assert.Contains(
                LoaderRegistry.Snapshot(),
                loader => loader.GetType().Assembly == externalAssembly
                          && loader.GetType().Name == "CompiledLoader");

            // The module lives here; the injectable's compiled binding lives in the
            // external assembly. Resolution succeeds only if both loaders participate.
            var container = Container.Create(typeof(WidgetModule));
            var widget = container.Get<Widget>();

            Assert.NotNull(widget);
            Assert.Equal("gadget", widget.Name);
            Assert.Equal(42, widget.Count);
        }

        [Module(Injects = new[] { typeof(Widget) })]
        public class WidgetModule
        {
            [Provides] public string Name() => "gadget";
            [Provides] public int Count() => 42;
        }
    }
}
