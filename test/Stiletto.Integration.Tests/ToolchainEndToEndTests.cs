using System.Linq;
using Stiletto;
using Stiletto.Integration.Tests.Sample;
using Xunit;

namespace Stiletto.Integration.Tests
{
    /// <summary>
    /// The true end-to-end test: unlike the API-driven generator tests, this project
    /// references <c>Stiletto.Generator</c> as an analyzer, so the generator is
    /// discovered via its <c>[Generator]</c> attribute and run by csc during this
    /// project's normal build. Nothing here is hand-instantiated — it exercises the
    /// full toolchain (analyzer load, generation, and natural [ModuleInitializer]
    /// registration) exactly as a real consumer would.
    /// </summary>
    public class ToolchainEndToEndTests
    {
        [Fact]
        public void GeneratorRanViaBuild_LoaderSelfRegistered_AndContainerResolves()
        {
            var thisAssembly = typeof(CoffeeMaker).Assembly;

            // (1) The generator ran during THIS project's compilation — the aggregated
            //     loader was emitted into this assembly.
            Assert.NotNull(thisAssembly.GetType("Stiletto.Generated.CompiledLoader"));

            // (2) Its [ModuleInitializer] fired naturally at assembly load (no
            //     RunModuleConstructor) — a compiled loader from this assembly is
            //     registered, so the container will use it before any reflection.
            Assert.Contains(
                LoaderRegistry.Snapshot(),
                loader => loader.GetType().Assembly == thisAssembly
                          && loader.GetType().Name == "CompiledLoader");

            // (3) End-to-end resolution through a real, statically-typed Container.
            var container = Container.Create(typeof(CoffeeModule));
            var maker = container.Get<CoffeeMaker>();

            Assert.NotNull(maker);
            Assert.NotNull(maker.Pump);              // constructor injection
            Assert.NotNull(maker.Pump.Heater);       // transitive
            Assert.Equal("Stiletto", maker.Brand);   // [Named] property from the module

            // [Singleton] Heater shared across resolutions.
            Assert.Same(maker.Pump.Heater, container.Get<CoffeeMaker>().Pump.Heater);
        }
    }
}
