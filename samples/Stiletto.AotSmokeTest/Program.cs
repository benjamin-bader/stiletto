using System;
using AotSmoke;
using Stiletto;

// Fails loudly if the reflection fallback is somehow still engaged.
if (Container.ReflectionFallbackEnabled)
{
    Console.Error.WriteLine("FAIL: reflection fallback is enabled; expected registry-only.");
    return 2;
}

var container = Container.Create(typeof(CoffeeModule));
var maker = container.Get<CoffeeMaker>();

if (maker.Pump?.Heater is null || maker.Brand != "Stiletto")
{
    Console.Error.WriteLine("FAIL: object graph not wired correctly.");
    return 1;
}

Console.WriteLine($"OK: resolved {maker.Brand} CoffeeMaker with {maker.Pump.Heater.GetType().Name} (registry-only, no reflection).");
return 0;
