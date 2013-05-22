Stiletto
========

A fast dependency injector in C# for .NET and Mono; please see [the introductory website][0] for more information.

This is a port of the [Square's Dagger IoC library][1], intended to be usable everywhere C# is usable, including MonoTouch where `System.Reflection.Emit` is unavailable.
Compile-time validation and code-generation is implemented as a Fody[1] weaver, to be installable via a NuGet package.

Users of Dagger, Guice, or any other javax.inject-compatible IoC container will feel at home:

```csharp
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
  public IPump MakePump(Thermosiphon pump)
  {
    return pump;
  }
}

public class CoffeeMaker
{
  [Inject] public IHeater Heater { get; set; }
  [Inject] public IPump Pump { get; set; }
}
```

Stiletto supports:
- property and constructor injection.
- disambiguation of identical types via `[Named("resourceName")]`
- the specification of classes and provider methods as `[Singleton]` resources.
- the specification of dependencies as Lazy<>

More is coming.

[0]: http://stiletto.bendb.com
[1]: http://square.github.io/dagger
