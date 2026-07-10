using Stiletto;

namespace Stiletto.Integration.Tests.Sample
{
    // A small P1-only graph: constructor injection (transitive), property injection
    // via a [Named] qualifier, a [Singleton], and a compiled module with [Provides].
    // No Lazy<T>/IProvider<T> — those still resolve via the reflection fallback (P2).

    [Singleton]
    public class Heater
    {
        [Inject] public Heater() { }
    }

    public class Pump
    {
        public readonly Heater Heater;

        [Inject] public Pump(Heater heater) { Heater = heater; }
    }

    public class CoffeeMaker
    {
        public readonly Pump Pump;

        [Inject] public CoffeeMaker(Pump pump) { Pump = pump; }

        [Inject, Named("brand")] public string Brand { get; set; }
    }

    [Module(Injects = new[] { typeof(CoffeeMaker) })]
    public class CoffeeModule
    {
        [Provides, Named("brand")] public string Brand() => "Stiletto";
    }
}
