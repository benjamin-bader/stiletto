using Stiletto;
using LibraryExample;

namespace Example
{
    [Module(
        EntryPoints = new[] { typeof(CoffeeApp) },
        IncludedModules = new[] { typeof(PumpModule), typeof(BeanModule) })]
    class DripCoffeeModule
    {
        [Provides, Singleton]
        public IHeater GetHeater()
        {
            return new ElectricHeater();
        }

        [Provides, Named("coffee-source")]
        public string GetCoffeeOrigin()
        {
            return "Ecuador";
        }
    }
}
