# Benchmarks

How Stiletto compares to other .NET DI containers, measured with
[BenchmarkDotNet]. The harness lives in `benchmarks/Stiletto.Benchmarks` and is
**not** part of `Stiletto.slnx` or CI (it pulls in the competitors as
dependencies); run it manually:

```
dotnet run -c Release --project benchmarks/Stiletto.Benchmarks -- --filter '*'
```

> The numbers below are illustrative short runs on a developer laptop
> (`--job short`), not certified figures — reproduce on a quiet machine for
> anything you intend to quote, and don't read meaning into sub-nanosecond or
> few-percent differences.

## What's compared, and the fairness rules

Two cohorts, chosen deliberately (see the note on categories at the end):

- **Runtime containers:** Microsoft.Extensions.DependencyInjection, Autofac, DryIoc.
- **Source-generated / AOT-safe containers** (Stiletto's true peers): Jab,
  Pure.DI, StrongInject.

DI containers differ wildly in capability, so we benchmark only the **capability
intersection**: plain constructor-injection object graphs. No decorators,
interception, child scopes, or conditional registration — Stiletto doesn't do
them, and it would be meaningless to measure containers on features the others
lack or leave idle.

The scenario is a "complex" graph (after Daniel Palme's [IocPerformance]): three
singletons, three transients, three services each depending on all six, and a
root pulling the services together. Resolving the root constructs **14 objects**
(root + 3 services + 10 transient instances), with the 3 singletons shared.

Two variants of the same shape:

- **Interface graph** — dependencies are interfaces. Stiletto binds them the
  idiomatic way, with `[Provides]` methods in a module; the others map
  `interface → impl`.
- **Concrete graph** — no interfaces. Stiletto uses pure `[Inject]`
  constructor bindings (no provider indirection); the others register each
  concrete type. This isolates raw construction cost.

**Equivalence is verified, not assumed.** A construction counter
(`dotnet run -c Release ... -- verify`) confirms every container builds exactly
10 transients. This caught a real harness bug: StrongInject's default
`[Register]` scope is `InstancePerResolution` (shared within a resolve), not true
transient — it was building 3, doing less work than the others, until pinned to
`Scope.InstancePerDependency`.

## Results (illustrative)

### Warm resolve — steady-state, container built and warmed once

Interface graph:

| Container | Mean | Allocated |
|---|--:|--:|
| Pure.DI | 43 ns | 400 B |
| Jab | 43 ns | 400 B |
| Microsoft.DI | 46 ns | 400 B |
| DryIoc | 48 ns | 400 B |
| StrongInject | 48 ns | 400 B |
| **Stiletto** | **124 ns** | **752 B** |
| Autofac | 1957 ns | 7856 B |

Concrete graph:

| Container | Mean | Allocated |
|---|--:|--:|
| Pure.DI | 23 ns | 208 B |
| Microsoft.DI | 27 ns | 208 B |
| Jab | 27 ns | 208 B |
| DryIoc | 27 ns | 208 B |
| StrongInject | 29 ns | 240 B |
| **Stiletto** | **115 ns** | **648 B** |
| Autofac | 1924 ns | 7808 B |

### Cold start — a fresh container built and the root resolved once (interface graph)

| Container | Mean | Allocated |
|---|--:|--:|
| Pure.DI | 54 ns | 472 B |
| Jab | 67 ns | 528 B |
| StrongInject | 125 ns | 864 B |
| **Stiletto** | **1.34 µs** | **5.8 KB** |
| DryIoc | 2.92 µs | 6.1 KB |
| Microsoft.DI | 4.51 µs | 14.2 KB |
| Autofac | 21.2 µs | 61.2 KB |

### Container build only — registration + Create/Build, no resolution (interface graph)

| Container | Mean | Allocated |
|---|--:|--:|
| Jab | 3 ns | 56 B |
| Pure.DI | 6 ns | 96 B |
| StrongInject | 24 ns | 360 B |
| DryIoc | 429 ns | 1.9 KB |
| **Stiletto** | **542 ns** | **4.2 KB** |
| Microsoft.DI | 836 ns | 7.4 KB |
| Autofac | 15.6 µs | 44.6 KB |

## What the numbers say (candidly)

- **Stiletto does not win warm throughput.** The compiled containers (both the
  source-gen peers and DryIoc/MS.DI) resolve in ~25–50 ns; Stiletto takes
  ~115–125 ns and allocates ~1.5–3× more. Only Autofac is slower.
- **The gap is inherent, not a modeling artifact.** Concrete-graph Stiletto
  (115 ns) is barely faster than interface-graph Stiletto (124 ns), so dropping
  `[Provides]` indirection isn't the issue. Stiletto resolves by walking a graph
  of `Binding` objects with virtual dispatch; the others run a flat, compiled
  factory (essentially one nested `new(...)`). That is the real cost, and it's a
  concrete **optimization opportunity** for Stiletto's generated bindings.
- **Where Stiletto genuinely beats the runtime containers: cold start.** With no
  delegate/expression to compile on first use, Stiletto reaches its first
  instance in ~1.3 µs vs DryIoc 2.9 / MS.DI 4.5 / Autofac 21. This matters for
  short-lived processes (CLIs, serverless, tests). The source-gen peers are
  faster still — their whole resolver is generated flat code.
- **The axis not shown here — NativeAOT / trimming — is Stiletto's real
  differentiator.** Under `PublishAot`, DryIoc/Autofac (and MS.DI's compiled
  path) can't emit or compile delegates and degrade or fail; Stiletto and the
  source-gen peers run with zero reflection. That comparison is a planned
  follow-up lane.

**Bottom line:** Stiletto's value is compile-time validation, AOT/trim safety,
and zero-reflection startup — not class-leading resolution speed. The
source-generated peers are meaningfully faster today; closing that warm-resolve
gap (flatter generated resolution, fewer per-resolve allocations) is the clearest
performance roadmap.

## A note on categories

Autofac and DryIoc are *runtime* containers (reflection + expression/IL
compilation) built for dynamic graphs; they're included for context, but they're
a different category. Stiletto's true peers are the *compile-time,
source-generated* containers — Jab, Pure.DI, StrongInject — which is where the
apples-to-apples comparison lives, and where Stiletto currently has the most to
gain.

[BenchmarkDotNet]: https://benchmarkdotnet.org
[IocPerformance]: https://github.com/danielpalme/IocPerformance
