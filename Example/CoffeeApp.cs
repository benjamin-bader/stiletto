using Abra;

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
            var app = container.Get<CoffeeApp>();
            app.Run();
        }
    }
}
