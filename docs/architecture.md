# How Stiletto works

A high-level tour of the moving parts, for contributors and the curious. If you
just want to *use* Stiletto, start with the [README](../README.md); for commit
and release conventions see [CLAUDE.md](../CLAUDE.md); for a deep dive on one
subsystem see [`docs/design/`](./design).

Stiletto is a compile-time dependency injector for .NET, ported from Square's
[Dagger]. Two things ship in one NuGet package:

- **`src/Stiletto`** — the runtime library (`net10.0`): the object-graph engine
  (`Container`, `Resolver`, `Binding`, …) plus the small attribute surface you
  annotate with.
- **`src/Stiletto.Generator`** — a Roslyn incremental source generator
  (`netstandard2.0`, as all analyzers must be) that runs in *your* build and
  emits the binding code. It has no runtime dependency on the library; it only
  emits text.

The design goal that shapes everything below: **work everywhere C# runs,
including NativeAOT and trimmed apps where runtime reflection and codegen aren't
available.** That's why the graph is wired at compile time instead of discovered
by reflection at startup.

## The two phases

```
BUILD TIME (in your compilation, via the bundled analyzer)
  [Inject] / [Module] / [Provides] / [Named] / [Singleton]
        │
        ▼  Stiletto.Generator
  ┌─────────────────────────────────────────────────────────────┐
  │ {Type}_CompiledBinding      one per injectable type          │
  │ {Module}_CompiledModule     one per [Module]                 │
  │ Stiletto.Generated.CompiledLoader   this assembly's ILoader  │
  │ registration + [assembly: StilettoLoaderAssembly]  (see below)│
  └─────────────────────────────────────────────────────────────┘

RUNTIME
  Container.Create(modules)
        │  snapshot LoaderRegistry  (+ reflection fallback, unless disabled)
        ▼
  RuntimeAggregationLoader ── Resolver ──> object graph
        │
        ▼
  container.Get<T>()
```

Nothing scans the filesystem or the AppDomain to *discover* your graph; the
generator has already turned your attributes into concrete `Binding` classes and
a loader, and those register themselves so `Container` can find them.

## The programming model

The attribute surface is intentionally small and mirrors Dagger / `javax.inject`
(full examples in the [README](../README.md)):

| Attribute | Meaning |
| --- | --- |
| `[Inject]` | A constructor or settable property to satisfy. |
| `[Module(Injects = …, IncludedModules = …)]` | A unit of bindings; `Injects` lists the entry-point types a container built from it must be able to produce. |
| `[Provides]` / `[Provides(ProvidesType.Set)]` | A factory method on a module; `Set` contributes to an `ISet<T>` multibinding. |
| `[Named("…")]` | Disambiguates two bindings of the same type (a *qualifier*). |
| `[Singleton]` | On a class or provider method: at most one instance per container. |

Dependencies may also be requested as `Lazy<T>` (deferred construction) or
`IProvider<T>` (repeatable construction).

## Build time: the source generator

`StilettoGenerator` is a single `[Generator]` with a few outputs, all driven off
the `[Inject]`/`[Module]` attributes via `ForAttributeWithMetadataName`:

- **Inject bindings** (`InjectBindingEmitter`): for each supported injectable
  type it emits `internal sealed class {Type}_CompiledBinding : Binding`, whose
  overrides construct the type, request its dependencies, and inject its
  properties (including chaining a non-framework base class's members).
- **Compiled modules** (`ModuleEmitter`): for each `[Module]`, a
  `{Module}_CompiledModule : RuntimeModule` whose `GetBindings` adds one nested
  `ProviderBinding_N` per `[Provides]` method (or contributes to a set via
  `SetBindings.Add<T>` for `ProvidesType.Set`).
- **The aggregate loader**: one `Stiletto.Generated.CompiledLoader : ILoader`
  per assembly that `switch`es on a type/module name and returns
  `new {…}_CompiledBinding()` / `new {…}_CompiledModule()`. `Lazy<T>` and
  `IProvider<T>` dependencies become concrete `LazyBinding<T>` /
  `ProviderBinding<T>` instantiations here.

Everything the generated code embeds is a **string key** (see below), computed
by `RoslynKeys` to be byte-for-byte identical to what the runtime `Key` class
produces from a `System.Type`. That equivalence is what lets compiled bindings
and (fallback) reflection bindings interoperate in one graph.

The generator emits **only the fully-correct subset** it can prove; anything it
skips (e.g. an unsupported shape) is left to the reflection fallback, so nothing
it emits is ever wrong.

## Runtime: the object-graph engine

- **`Key`** — everything is addressed by an ordinal string key, e.g.
  `Sample.CoffeeMaker`, `@main/Sample.Pump` (qualified), `members/Sample.Widget`
  (property injection), or the backtick-arity generic form
  `System.Collections.Generic.IList\`1<Sample.Bean>`. `Lazy<T>`, `IProvider<T>`,
  and `ISet<T>` are recognized by key prefix.
- **`Binding`** — the unit of construction. Key methods: `Resolve` (request its
  dependencies from the resolver), `Get` (produce the instance),
  `GetDependencies` (report edges for validation), `InjectProperties`.
  Subclasses include the generated `_CompiledBinding`s, the reflection bindings,
  `SingletonBinding` (memoizing wrapper), and the `Lazy`/`Provider`/`Set`
  bindings.
- **`Resolver`** — builds the graph by working a queue of bindings, resolving
  each key to a `Binding` (creating "just-in-time" bindings on demand via the
  loader, using `DeferredBinding` to break request cycles). `[Singleton]`
  bindings get wrapped in `SingletonBinding` here.
- **`RuntimeModule`** — the runtime view of a `[Module]`: its `Injects` keys,
  included modules, flags (`IsLibrary`, `IsComplete`, `IsOverride`), and a
  `GetBindings` that installs the module's provider bindings.
- **`Container`** — the public entry point. `Container.Create(params object[])`
  instantiates the modules, snapshots the available loaders once into a
  `RuntimeAggregationLoader`, and resolves against that fixed set for the
  container's lifetime. `Get<T>()` / `Inject<T>(instance)` walk the resolved
  bindings; `Validate()` runs `GraphVerifier` for eager, whole-graph checking.

## Loaders and registration

An `ILoader` answers "give me the binding/module for this key." At runtime a
`RuntimeAggregationLoader` consults loaders in order and takes the first
non-null answer:

1. **Source-generated `CompiledLoader`s**, collected from `LoaderRegistry` — the
   fast, reflection-free, AOT-safe path.
2. *(optional fallback)* **`CodegenLoader`** — finds generated
   `{Type}_CompiledBinding` types by name via reflection (`ReflectionUtils`),
   for assemblies whose loader didn't self-register.
3. *(optional fallback)* **`ReflectionLoader`** — builds bindings purely by
   reflecting over the attributes, for anything not compiled at all.

The fallback (2 + 3) is controlled by `Container.ReflectionFallbackEnabled`,
backed by the `Stiletto.ReflectionFallback` feature switch (**on by default**).
Turn it off — e.g. `<RuntimeHostConfigurationOption Include="Stiletto.ReflectionFallback" Value="false" Trim="true" />` —
for a **registry-only** container: the trimmer/NativeAOT removes the reflection
code entirely, and a missing binding fails loudly instead of silently falling
back to reflection. The switch is annotated with `[FeatureSwitchDefinition]` /
`[FeatureGuard]` so the trimmer can fold it and the analyzer understands the
guarded reflection paths.

### How compiled loaders register — and the cross-assembly subtlety

Each producer assembly's `CompiledLoader` self-registers via a
`[ModuleInitializer]`. Module initializers run *lazily* — only when the CLR is
about to touch a type in that module — which is a trap: an assembly that
contributes bindings but is never referenced in code before `Container.Create`
runs won't have registered its loader in time, and (registry-only) that's a hard
failure.

The fix: the generator also treats any assembly that **calls `Container.Create`**
(and every executable) as an *anchor*. In an anchor it scans the compile-time
reference closure for the `[assembly: StilettoLoaderAssembly]` marker and emits a
single **eager** aggregate `[ModuleInitializer]` (`__ReferencedLoaders`) that
force-registers every referenced assembly's loader before any container
snapshots the registry — a CLR-guaranteed happens-before, not touch-order luck.
Assemblies loaded dynamically at runtime (plugins, `Assembly.LoadFrom`) are the
one thing the generator can't see; those call `LoaderRegistry.Register(...)`
explicitly.

The full rationale, failure scenarios, and edge cases live in
[`docs/design/cross-assembly-loader-registration.md`](./design/cross-assembly-loader-registration.md).

## NativeAOT / trimming

Because the graph is compiled, the resolved path uses zero reflection: `new`
expressions on concrete types and a `switch` over string keys. With the
reflection fallback switched off, the reflection loaders trim away and Stiletto
publishes cleanly under `PublishAot`. `samples/Stiletto.AotSmokeTest` is a
NativeAOT console app that CI publishes and runs on every push as a hard gate.

## Packaging

The `Stiletto` NuGet package bundles both halves: the runtime assembly under
`lib/`, and the generator under `analyzers/dotnet/cs/` so it runs automatically
in consumers' builds. There is no separate plugin (this replaced the old
Fody/IL-weaving `Stiletto.Fody` package). Releases are automated with
release-please; see [CLAUDE.md](../CLAUDE.md).

## Repository map

| Path | What's there |
| --- | --- |
| `src/Stiletto` | Runtime library (`net10.0`). |
| `src/Stiletto.Generator` | The Roslyn source generator (`netstandard2.0`). |
| `test/Stiletto.Tests` | Runtime unit tests. |
| `test/Stiletto.Generator.Tests` | Generator snapshot + behavior tests (Verify). |
| `test/Stiletto.Integration.Tests` | End-to-end: compile generated bindings and resolve through a real `Container`. |
| `test/Stiletto.Integration.External` | A separate assembly the integration tests reference, so cross-assembly loader registration is actually exercised. |
| `samples/` | Runnable samples, including `Stiletto.AotSmokeTest` (the NativeAOT gate). |
| `docs/design/` | Deep-dive design docs for individual subsystems. |

[Dagger]: https://square.github.io/dagger
