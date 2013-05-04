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
            // prime the JIT
            Container.Create(new DripCoffeeModule()).Get<CoffeeApp>();

            Console.Write("100000 iterations (three gets):    ");
            var sw = new Stopwatch();
            sw.Start();
            OneGet();

            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);
            
            /*Console.Write("10000 iterations (one create, three gets): ");

            sw.Reset();
            sw.Start();
            OneGet();
            sw.Stop();

            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);*/
        }

        private static void OneGet()
        {
            for (var i = 0; i < 100000; ++i)
            {
                var container = Container.Create(new DripCoffeeModule());
                container.Get<CoffeeApp>().ToString();
            }
        }

        private static void ThreeGets()
        {
            var container = Container.Create(new DripCoffeeModule());
            for (var i = 0; i < 100000; ++i) {
                container.Get<CoffeeApp>();
                container.Get<CoffeeApp>();
                container.Get<CoffeeApp>();
            }
        }
    }
}
