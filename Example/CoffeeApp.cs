using System;
using System.Diagnostics;
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
            Test();
            var container = Container.Create(new DripCoffeeModule());
            for (var i = 0; i < 100000; ++i) {
                container.Get<CoffeeApp>();
                container.Get<CoffeeApp>();
                container.Get<CoffeeApp>();
            }
        }

        static void Test()
        {
            var container = Container.Create(new DripCoffeeModule());
            container.Get<CoffeeApp>();

            var sw = new Stopwatch();

            var hash = 0;
            sw.Start();
            for (var i = 0; i < 10000; ++i) {
                container = Container.Create(new DripCoffeeModule());
                hash += container.Get<CoffeeApp>().GetHashCode();
            }
            sw.Stop();

            Console.WriteLine("Hash {0}, {1} iters, {2} ms", hash, 10000, sw.ElapsedMilliseconds);
        }
    }
}
