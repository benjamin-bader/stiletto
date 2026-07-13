# Eager cross-assembly loader registration

**Status:** proposed
**Applies to:** `src/Stiletto.Generator`, `src/Stiletto` (runtime `LoaderRegistry` / `Container`)

## Summary

Stiletto's per-assembly source-generated loaders self-register through a
`[ModuleInitializer]`. Module initializers only run once the CLR is *about to
touch a type in that module*, so an assembly that contributes bindings but is
never touched before `Container.Create` runs will not have registered its
loader in time. Under the registry-only configuration (reflection fallback
disabled — the trimming/NativeAOT story) that is a hard resolution failure.

This document describes the gap precisely and proposes a codegen fix: during
compilation, the generator scans the reference closure of any assembly that
**calls `Container.Create`** (plus the entry executable) and emits a single
eager aggregate `[ModuleInitializer]` that force-registers every referenced
Stiletto loader before any container is built.

## Background: how registration works today

- The generator emits, per assembly that has any `[Inject]`/`[Module]` types,
  one `Stiletto.Generated.CompiledLoader` and a `CompiledLoaderRegistration`
  whose `[ModuleInitializer]` calls `LoaderRegistry.Register(new CompiledLoader())`.
- `Container.Create(params object[] modules)` takes a **one-time snapshot** of
  `LoaderRegistry`, wraps it in a `RuntimeAggregationLoader`, and resolves
  against that fixed set for the container's lifetime. Nothing re-reads the
  registry afterwards.
- Resolution is keyed by **strings**. `RuntimeModule.Injects` is `string[]`;
  provider return/parameter types and `[Inject]`-constructor dependencies flow
  as string `Key`s; `CompiledLoader.GetInjectBinding`/`GetRuntimeModule`
  `switch` on `className` / `moduleType.FullName`. A loader only answers for
  types defined in *its own* assembly.

## Root cause

The only assemblies **guaranteed** to be in the snapshot are those whose module
type/instance is passed *literally as an argument* to `Container.Create(...)`.
That argument is an `ldtoken typeof(X)` (or `newobj X`) that executes at the
call site, in the caller's IL, **before** `Create`'s body — and therefore
before the snapshot. Everything else a container needs is discovered by string
key *after* the snapshot, and nothing in that path executes a token that would
force a contributing assembly to load:

| Edge | Representation | Touches the other assembly before the snapshot? |
| --- | --- | --- |
| Module passed to `Create` | `ldtoken` / `newobj` at call site | **Yes** (only guaranteed case) |
| `Injects` | `string[]` | No |
| `[Provides]` return / parameter deps | string `Key` | No |
| `[Inject]`-ctor deps | string `Key` | No |
| `IncludedModules` | `Type[]` via `typeof`, evaluated *inside* `Create` | Touched, but after the snapshot |

Because the snapshot is taken once and frozen, any loader that registers after
it — or never, because its assembly was never touched — is invisible to that
container permanently.

## Failure scenarios

1. **Cross-assembly injectable (the classic layout).** Assembly `App` has
   `[Module(Injects = new[]{ typeof(Service) })]`; `Service` (with an `[Inject]`
   ctor) lives in `Lib`. `Container.Create(typeof(AppModule)).Get<Service>()`
   never executes a token for any `Lib` type, so `Lib`'s loader never registers
   in time. Registry-only → `GetInjectBinding("Lib.Service")` returns `null!` →
   graph error. This is exactly the ported `CanInjectCrossAssemblies` shape
   (`test/Stiletto.Integration.External/Widget.cs`).

2. **Cross-assembly included module.** `AppModule` includes a module type from
   `Lib`. The `typeof` in `Includes` force-loads `Lib` during the walk, but
   *after* the snapshot, so its loader isn't in this container.

3. **Order-dependent double-create (the subtle one).** A container created
   before `Lib` is touched has a frozen snapshot without `Lib`; a structurally
   identical `Create` call made after something else touched `Lib` succeeds.
   Same code, different outcome based on incidental touch order — the kind of
   non-reproducible bug that escapes tests.

### Where it bites, where it's masked

- **Masked — single-assembly apps** (the common case): everything lives in one
  assembly; naming any module touches it and its single loader carries every
  binding.
- **Masked — reflection fallback ON (default):** a missing compiled binding
  falls through to the reflection loaders, which resolve by name (loading the
  assembly as a side effect). Correct result, but the compiled/AOT path is
  silently lost.
- **Hard failure — registry-only + trimming/NativeAOT:** the fallback code is
  trimmed away, and AOT does not change module-initializer timing (initializers
  still run lazily "before first access to the module's types"). This is
  precisely the configuration the registry-only design exists to serve.

## Goals / non-goals

**Goals**

- Every loader a `Container.Create` call can need is registered *before* the
  call runs, with a CLR-guaranteed ordering — no dependence on incidental touch
  order.
- Fully static / trim- / AOT-safe: no reflection, no `Type.GetType`.
- No change to the client experience (`Container.Create(typeof(Module))`).
- Cheap and incremental in the generator.

**Non-goals**

- Covering assemblies that are **not** in the compile-time reference closure
  (reflection-loaded plugins via `Assembly.LoadFrom`, MEF, etc.). Those remain
  the domain of the explicit `LoaderRegistry.Register` escape hatch — an
  irreducible boundary: if no compiled assembly references it, no generator can
  see it.

## Design

Three emit paths. (1) is an extension of what exists today; (2) and (3) are new
and, crucially, **must fire even when the emitting assembly defines no bindings
of its own** — a composition-root library typically has none.

### 1. Producer side — every assembly that has bindings

In addition to today's internal `CompiledLoader` + self-`[ModuleInitializer]`,
emit:

- An assembly-level marker attribute advertising a **public, uniquely-named**
  registration entry point, so consumers can discover and call it:

  ```csharp
  [assembly: global::Stiletto.StilettoLoaderAssembly("Stiletto.Generated.Registrations.LibFoo")]
  ```

- The public registrar. The name embeds the assembly to avoid the `CS0433`
  collision that a shared fully-qualified name would cause across references:

  ```csharp
  namespace Stiletto.Generated.Registrations
  {
      public static class LibFoo                       // unique per assembly
      {
          public static void EnsureRegistered()
              => global::Stiletto.LoaderRegistry.Register(
                     new global::Stiletto.Generated.CompiledLoader());

          [global::System.Runtime.CompilerServices.ModuleInitializer]
          internal static void Init() => EnsureRegistered();  // direct-touch path
      }
  }
  ```

  The registrar is deliberately **stateless** — no double-checked `registered`
  flag. Idempotency lives entirely in `LoaderRegistry.Register`, which dedups by
  loader `Type` under its lock. A flag would reintroduce a memory-model hazard:
  because different assemblies' module initializers run concurrently on
  different threads, one thread could observe `registered == true` set by
  another *before* that other thread's `Register` completed, return early, and
  then snapshot the registry with the loader still missing. Routing every call
  through the lock removes that hazard: each anchor registers every loader in
  its closure itself, through `lock(loaders)`, before its own `Create` — so its
  snapshot is guaranteed to see them, independent of other threads. Redundant
  calls (the producer's own `Init`, other consumers) coalesce to a single
  registration via the type dedup. The tiny cost is a throwaway
  `CompiledLoader` allocation per redundant call, discarded by the dedup.

Keeping the per-assembly `[ModuleInitializer]` preserves correct behavior for
libraries consumed by an app that does **not** run the Stiletto generator.

### 2. Anchor side — every assembly that calls `Container.Create`

Detect call sites of `Container.Create` / `CreateWithLoaders` with a
`SyntaxProvider` over invocations named `Create`, semantic-checked against
`Stiletto.Container`. In any assembly that has such a call, scan the
compile-time reference closure (`compilation.SourceModule.ReferencedAssemblySymbols`,
filtered by the `StilettoLoaderAssembly` marker) and emit one aggregate:

```csharp
namespace Stiletto.Generated
{
    internal static class __ReferencedLoaders
    {
        [global::System.Runtime.CompilerServices.ModuleInitializer]
        internal static void RegisterAll()
        {
            global::Stiletto.Generated.Registrations.LibFoo.EnsureRegistered();
            global::Stiletto.Generated.Registrations.LibBar.EnsureRegistered();
            // …one call per marked referenced assembly, sorted for reproducible builds
        }
    }
}
```

**Why the `Create` call site is the correct — and airtight — anchor.** The CLR
runs a module's initializer *before the first access to any type or method in
that module*. So the initializer of the assembly that contains the `Create`
call is guaranteed to run before that assembly's own code — i.e. before the
`Create` call's IL executes. This is a genuine happens-before, not touch-order
luck, and it holds whether the caller is the exe or a library.

The reference closure of the calling assembly is a **superset** of what that
call can need: to name (or construct) the modules it passes, the assembly must
reference their assemblies; those modules' own dependency assemblies come in as
transitive references, which the SDK includes in the compilation's reference
set. Registering the whole closure therefore covers every statically reachable
binding.

### 3. Catch-all — the entry executable

Also emit the aggregate when `compilation.Options.OutputKind` is an application,
regardless of whether the exe itself calls `Create`. This covers the
**opaque-delegation** case that the call-site anchor alone misses:

```csharp
void Bootstrap(object[] modules) => Container.Create(modules);   // in a generic lib
```

Here the bootstrap library's static closure need not include the concrete
module assemblies — the exe constructed those instances and handed them over.
The exe *does* reference them, so anchoring at the entry assembly closes the
gap. The union of (2) and (3) covers everything statically knowable.

### Residual gap

Modules whose assemblies are known **nowhere** at compile time (reflection- or
plugin-loaded) remain uncovered by design. The story becomes clean and
teachable:

- **Referenced at build time → automatic.**
- **Dynamically loaded → one explicit `LoaderRegistry.Register(...)` call.**

### Optional runtime hardening (secondary, not a substitute)

Independently, `Container` could stop freezing the snapshot forever: on a
resolution **miss**, re-read `LoaderRegistry` and retry once before erroring
(or resolve against a live view). This gracefully absorbs late/dynamic
registrations, but by itself reintroduces touch-order sensitivity and cannot
conjure an assembly that was never touched. Treat it as cheap insurance layered
on top of the codegen fix, not as the fix.

## Consequences / trade-offs

- **Eager registration.** Anchoring at `Create` callers means only assemblies
  that actually build containers pay the cost of instantiating every referenced
  loader up front. Leaf binding libraries keep their lazy per-assembly
  `[ModuleInitializer]` for the direct-touch path. Loader construction is a
  cheap `new CompiledLoader()` added to a list.
- **New public surface.** `StilettoLoaderAssembly` and the generated
  `Stiletto.Generated.Registrations.*` types become public API of each compiled
  assembly. Introducing the attribute is a **`feat`** (minor bump). The
  generated registrar names are an implementation detail but are, technically,
  public.
- **Incrementality.** The reference scan must be projected to a small, equatable
  model (marked assembly identity → entry-point name) so `RegisterSourceOutput`
  is cached and only recomputes when references change, not on every keystroke.
- **Determinism.** Emit `EnsureRegistered()` calls in a stable order (sorted by
  assembly name) for reproducible builds.

## Testing strategy

The behavior under test is process-global and sticky: once *any* code in a
process touches a contributing assembly, its `[ModuleInitializer]` has fired and
its loader is registered for the remainder of the process. A naive "assert not
registered" test is therefore order-dependent and unreliable inside a shared
test process.

Reliable approaches, in preference order:

1. **Runtime-compiled, precisely-touched assemblies** — extend the harness in
   `test/Stiletto.Generator.Tests/ModuleInitializerTimingTests.cs`, which
   already compiles throwaway assemblies from source and controls exactly when
   each is touched via `Assembly.Load(bytes)` + a resolve hook. To exercise real
   generated loaders it must run the generator over those compilations (driver
   API), not just `CSharpCompilation.Emit`.
2. **A dedicated never-touched external assembly** plus the
   `Stiletto.ReflectionFallback` `AppContext` switch set to `false`, asserting
   the resolution failure — but this is only sound if the test owns its own
   process (no other test may touch that assembly first).

The failing test is the executable spec for the fix: registry-only container, a
contributing assembly not touched before `Create`, expect the current
resolution error; after the fix, expect success. Then add the positive
cross-assembly counterpart to `Stiletto.Integration.Tests` that does **not**
pre-touch the external assembly (unlike today's `CrossAssemblyTests`, which
deliberately touches `typeof(Widget).Assembly` first).

## Open questions

- Do we want an MSBuild property to opt out of the aggregate (e.g. for an
  assembly that intentionally manages registration by hand)?
- Is detecting `Create` via the semantic model worth the cost versus simply
  always emitting the aggregate in any assembly whose closure contains a marked
  loader assembly? (The former scopes eager registration more tightly; the
  latter is simpler and still correct.)
