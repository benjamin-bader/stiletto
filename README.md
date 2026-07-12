Stiletto
========

_NOTE:_ This project is no longer being actively developed, as I no longer do mobile C# development.  If someone wants to take over, please let me know!

A fast dependency injector in C# for .NET and Mono; please see [the introductory website][0] for more information.

This is a port of the [Square's Dagger IoC library][1], intended to be usable everywhere C# is usable, including NativeAOT and trimmed apps where reflection is unavailable.
Compile-time validation and code-generation is implemented as a Roslyn source generator that ships in the same NuGet package as the runtime.

Users of Dagger, Guice, or any other javax.inject-compatible IoC container will feel at home:

```csharp
[Module(
  Injects = new[] { typeof(CoffeeMaker) })]
public class CoffeeModule
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

To install Stiletto and start using it, add the Stiletto NuGet package to your project, and start injecting:

```
dotnet add package Stiletto
```

The package bundles both the runtime and the source generator, so the compile-time validation and binding generation are wired up automatically — there is no separate plugin to install.

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
  [Provides, Named("this-is-a-dep")]
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
}

// [Named] works the same way on constructor parameters:
public class AlsoNeedsTwoStrings
{
  [Inject]
  public AlsoNeedsTwoStrings(
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
    return random.Next();
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
[Module(Injects = new[] { typeof(SetEntryPoint) })]
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

# Upgrading from the Fody-based Stiletto (0.x)

Stiletto 1.0 replaces the old Fody/IL-weaving pipeline with a Roslyn source
generator. Older versions rewrote your assemblies after compilation with a
`Stiletto.Fody` weaver; 1.0 emits the same binding code at compile time from a
generator bundled inside the `Stiletto` package.

To upgrade:

- Remove the `Stiletto.Fody` package reference and delete Stiletto's entry from
  `FodyWeavers.xml` (drop `Fody` entirely if nothing else uses it).
- Keep (or add) the `Stiletto` package. The generator runs automatically — there
  is no weaver or plugin to configure.
- Note that 1.0 targets `net10.0` only, and now supports NativeAOT and trimmed
  apps (with an optional reflection fallback behind a feature switch).

The public API is unchanged — `[Inject]`, `[Module]`, `[Provides]`, `[Named]`,
`[Singleton]`, and `Container.Create(...)` all behave as before — so application
code that used those should not need changes.

# Building

Stiletto targets a single, modern toolchain — the .NET SDK pinned in `global.json`. Building and testing is just:

```
dotnet build Stiletto.slnx --configuration Release
dotnet test Stiletto.slnx --configuration Release
```

The `Stiletto` package (runtime plus source generator) is produced with:

```
dotnet pack src/Stiletto/Stiletto.csproj --configuration Release
```

Releases are automated with [release-please](https://github.com/googleapis/release-please). Conventional-commit history drives a release PR that bumps the version and updates the changelog; merging it tags the release and publishes the package to nuget.org via OIDC Trusted Publishing (no long-lived API-key secret). See `CLAUDE.md` for the commit conventions.

# Testing

The test suite lives under `test/` and runs with `dotnet test`:
* `Stiletto.Tests` — the runtime library.
* `Stiletto.Generator.Tests` — snapshot tests for the source generator's output.
* `Stiletto.Integration.Tests` — end-to-end tests that compile and exercise generated bindings.

A NativeAOT smoke test (`samples/Stiletto.AotSmokeTest`) publishes with the reflection fallback disabled, verifying that the generator-only path trims and runs cleanly. CI runs it on every push.


[0]: http://stiletto.bendb.com
[1]: http://square.github.io/dagger
