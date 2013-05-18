Stiletto
========

A C# port of Square's Dagger IoC library: [http://square.github.io/dagger].

This is a vanilla port, intended to be usable everywhere C# is usable, including MonoTouch where `System.Reflection.Emit` is unavailable.
As of this writing, some features (namely Lazy and IProvider injections) will not work on MonoTouch; once compile-time codegen is complete, this restriction will be lifted.

Users of Dagger, Guice, or any other javax.inject-compatible IoC container will feel only slightly uncomfortable at the syntax:

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
