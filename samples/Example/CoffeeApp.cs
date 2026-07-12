using Stiletto;

namespace Example
{
    class CoffeeApp
    {
        [Inject]
        public CoffeeMaker CoffeeBot { get; set; }

        public void Run()
        {
            CoffeeBot.Brew();
        }

        static void Main()
        {
            var container = Container.Create(new DripCoffeeModule());
            container.Get<CoffeeApp>().Run();
        }
    }
}
