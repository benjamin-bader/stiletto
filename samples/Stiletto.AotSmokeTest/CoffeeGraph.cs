using Stiletto;

namespace AotSmoke
{
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
