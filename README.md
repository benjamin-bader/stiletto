abra-ioc
========

A C# port of Square's Dagger IoC library: [https://square.github.io/dagger].

This is a vanilla port, intended to be usable everywhere C# is usable, including MonoTouch where `System.Reflection.Emit` is unavailable.

Users of Dagger, Guice, or any other javax.inject-compatible IoC container will feel only slightly uncomfortable at the syntax:

```
[Module(
  EntryPoints = new[] { typeof(CoffeeApp) })]
public class CoffeeMoule
{
  [Provides]
  public IHeater MakeHeater()
  {
    return new ElectricHeater();
  }

  [Provides]
  public IPump(Thermosiphon pump)
  {
    return pump;
  }
}

public class CoffeeApp
{
  [Inject] public IHeater Heater { get; set; }
  [Inject] public IPump Pump { get; set; }
}
```

Abra supports:
- property and constructor injection.
- disambiguation of identical types via `[Named("resourceName")]`
- the specification of classes and provider methods as `[Singleton]` resources.

More is coming.
