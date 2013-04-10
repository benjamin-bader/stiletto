using Abra;

namespace Example
{
    [Module(
        EntryPoints = new[] { typeof(CoffeeApp) },
        IncludedModules = new[] { typeof(PumpModule) })]
    class DripCoffeeModule
    {
        [Provides, Singleton]
        public IHeater GetHeater()
        {
            return new ElectricHeater();
        }
    }
}
