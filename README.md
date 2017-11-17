Stiletto
========

_NOTE:_ This project is no longer being actively developed, as I no longer do mobile C# development.  If someone wants to take over, please let me know!

A fast dependency injector in C# for .NET and Mono; please see [the introductory website][0] for more information.

This is a port of the [Square's Dagger IoC library][1], intended to be usable everywhere C# is usable, including MonoTouch where `System.Reflection.Emit` is unavailable.
Compile-time validation and code-generation is implemented as a [Fody][2] weaver, installable via a NuGet package.

Users of Dagger, Guice, or any other javax.inject-compatible IoC container will feel at home:

```csharp
[Module(
  Injects = new[] { typeof(CoffeeApp) })]
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

# Getting Started

To install Stiletto and start using it, add the Stiletto NuGet package to your project, and start injecting.
To install the compile-time plugin, install the Stiletto.Fody NuGet package in your main project.

Stiletto supports:
- property and constructor injection.
- disambiguation of identical types via `[Named("resourceName")]`
- the specification of classes and provider methods as `[Singleton]` resources.
- the specification of dependencies as Lazy<>

## Constructor Injection

```csharp
public class CoffeeMaker
{
  private readonly IHeater heater;

  [Inject]
  public CoffeeMaker(IHeater heater)
  {
    this.heater = heater;
  }
}
```

## Property Injection 

```csharp
public class CoffeeMaker
{
  [Inject]
  public IHeater Heater { get; set; }
}
```

## Named Dependencies

```csharp
[Module(Injects = new[] { typeof(NeedsTwoStrings) })]
public class NamedDependencyModule
{
  [Provides, Named("this-is-one-dep")]
  public string ProvideStringOne()
  {
    return "foo";
  }

  [Provides, Named("this-is-another-dep")]
  public string ProvideStringTwo()
  {
    return "bar";
  }
}

public class NeedsTwoStrings
{
  [Inject, Named("this-is-a-dep")]
  public string StringOne { get; set; }

  [Inject, Named("this-is-another-dep")]
  public string StringTwo { get; set; }

  // Also works for constructors
  public NeedsTwoStrings(
      [Named("this-is-a-dep")] string stringOne,
      [Named("this-is-another-dep")] string stringTwo)
  {
  }
}

```

## Singletons

There are two ways to indicate singleton scope:

On a class...

```csharp
[Singleton]
public class WebService : IWebService
{
  // ...
}
```

...or on a provider method

```csharp
[Module(...)]
public class HasSingletonModule
{
  [Provides, Singleton]
  public ISettings ProvideSettings()
  {
    // Will only be called once
    return FileSettings.Read(...);
  }
}
```

## Lazy and IProvider Injections

Stiletto can wrap your dependencies in Lazy<T> to defer loading, or IProvider<T> to give you more than one instance of a dependency:

```csharp
[Module(Injects = new[] { typeof(EntryPoint) })]
public class EagerModule
{
  private readonly Random random = new Random();

  [Provides]
  public ISettings ProvideSettings()
  {
    Console.WriteLine("Reading from file now");
    return FileSettings.Read(...);
  }
  
  [Provides]
  public int ProvideRandomNumber()
  {
    return random.NextInt();
  }
}

public class EntryPoint
{
  [Inject]
  public Lazy<ISettings> Settings { get; set; }
  
  [Inject]
  public IProvider<int> RandomNumbers { get; set; }
}

var entryPoint = Container.Create(typeof(EagerModule)).Get<EntryPoint>();
Console.WriteLine("entryPoint is injected");
Debug.Assert(entryPoint.Settings.Value != null); // "Reading from file now"
var nums = entryPoint.RandomNumbers;
Console.WriteLine("Numbers: {0} {1} {2}", nums.Get(), nums.Get(), nums.Get()); // "Numbers: 3 19 36"
```

## Multibindings

Stiletto supports multibindings in the form of `ISet<T>`.  Multiple `[Provides]` methods can contribute to the same set, as follows:

```csharp
[Module(Injects = new[] { typeof(SetEntryPoint) )]
public class SetModule
{
  [Provides(ProvidesType.Set)]
  public string ProvideStringOne()
  {
    return "foo";
  }
  
  [Provides(ProvidesType.Set)]
  public string ProvideStringTwo()
  {
    return "bar";
  }
}

public class SetEntryPoint
{
  [Inject]
  public ISet<string> Strings { get; set; }
}
```

# Building

It's not easy to make a full build of Stiletto, but building for one platform is easy; requirements vary per platform:
* Mono, .NET 4: Vanilla .NET builds can be made with your favorite .NET toolchain
* iOS: A Xamarin iOS account is required, and either Xamarin Studio or Visual Studio must be used to build.  No Mac is required.
* Android: A Xamarin Android account is required, and either Xamarin Studio or Visual Studio must be used to build.
* Windows Phone 8: Visual Studio 2012 on Windows 8 with the WP8 SDK installed must be used to build.
* Everything: The union of the above: Windows 8, VS 2012, Xamarin accounts.
* NuGet: Packing and pushing are manual, but building the NuGet.csproj file will assemble all dependencies in a NuGetBuild directory; its subdirectories are the Stiletto and Stiletto.Fody convention-based directories.

# Testing

Unit tests can be run with your favorite NUnit runner.  Stiletto.Test contains the definitive set of unit tests covering the Stiletto common library.  Stiletto.Test.PostWeaving contains the same tests, but building it also builds the Fody weaver and runs the test assembly through the weaving process.  While not a complete integration test, this ensures that any changes to the weaver don't cause behavior changes at runtime.

There is a suite of integration tests covering the compile-time code validation and generation features.  Each test is a separate C# project containing code to be validated along with a file describing the expected outcome.  They can be run en suite by the `ValidateBuilds` tool.  Once built, it will be copied to the IntegrationTests folder.  Run it from there, and it will build and verify each test case.


[0]: http://stiletto.bendb.com
[1]: http://square.github.io/dagger
[2]: https://github.com/Fody/Fody
