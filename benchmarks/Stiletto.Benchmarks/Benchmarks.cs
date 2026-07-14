using Autofac;
using BenchmarkDotNet.Attributes;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Stiletto.Benchmarks.Concrete;

namespace Stiletto.Benchmarks;

// Steady state: containers built + warmed once, then the root resolved repeatedly.
[MemoryDiagnoser]
public class WarmResolve
{
    private global::Stiletto.Container _stiletto = null!;
    private ServiceProvider _msdi = null!;
    private global::Autofac.IContainer _autofac = null!;
    private global::DryIoc.Container _dryioc = null!;
    private JabProvider _jab = null!;
    private PureComposition _pure = null!;
    private StrongInjectContainer _si = null!;

    [GlobalSetup]
    public void Setup()
    {
        _stiletto = Adapters.BuildStiletto();
        _msdi = Adapters.BuildMsdi();
        _autofac = Adapters.BuildAutofac();
        _dryioc = Adapters.BuildDryIoc();
        _jab = SourceGenAdapters.BuildJab();
        _pure = SourceGenAdapters.BuildPure();
        _si = SourceGenAdapters.BuildStrongInject();

        _ = _stiletto.Get<ComplexRoot>();
        _ = _msdi.GetRequiredService<ComplexRoot>();
        _ = _autofac.Resolve<ComplexRoot>();
        _ = _dryioc.Resolve<ComplexRoot>();
        _ = SourceGenAdapters.ResolveJab(_jab);
        _ = SourceGenAdapters.ResolvePure(_pure);
        _ = SourceGenAdapters.ResolveStrongInject(_si);
    }

    [Benchmark(Baseline = true)] public ComplexRoot Stiletto() => _stiletto.Get<ComplexRoot>();
    [Benchmark] public ComplexRoot MicrosoftDI() => _msdi.GetRequiredService<ComplexRoot>();
    [Benchmark] public ComplexRoot Autofac() => _autofac.Resolve<ComplexRoot>();
    [Benchmark] public ComplexRoot DryIoc() => _dryioc.Resolve<ComplexRoot>();
    [Benchmark] public ComplexRoot Jab() => SourceGenAdapters.ResolveJab(_jab);
    [Benchmark] public ComplexRoot PureDI() => SourceGenAdapters.ResolvePure(_pure);
    [Benchmark] public ComplexRoot StrongInject() => SourceGenAdapters.ResolveStrongInject(_si);
}

// Cold start: a fresh container built and the root resolved once, every invocation.
[MemoryDiagnoser]
public class ColdStart
{
    [Benchmark(Baseline = true)] public ComplexRoot Stiletto() => Adapters.BuildStiletto().Get<ComplexRoot>();
    [Benchmark] public ComplexRoot MicrosoftDI() => Adapters.BuildMsdi().GetRequiredService<ComplexRoot>();
    [Benchmark] public ComplexRoot Autofac() => Adapters.BuildAutofac().Resolve<ComplexRoot>();
    [Benchmark] public ComplexRoot DryIoc() => Adapters.BuildDryIoc().Resolve<ComplexRoot>();
    [Benchmark] public ComplexRoot Jab() => SourceGenAdapters.ResolveJab(SourceGenAdapters.BuildJab());
    [Benchmark] public ComplexRoot PureDI() => SourceGenAdapters.ResolvePure(SourceGenAdapters.BuildPure());
    [Benchmark] public ComplexRoot StrongInject() => SourceGenAdapters.ResolveStrongInject(SourceGenAdapters.BuildStrongInject());
}

// Container construction only (registration + Create/Build), no resolution.
[MemoryDiagnoser]
public class ContainerBuild
{
    [Benchmark(Baseline = true)] public object Stiletto() => Adapters.BuildStiletto();
    [Benchmark] public object MicrosoftDI() => Adapters.BuildMsdi();
    [Benchmark] public object Autofac() => Adapters.BuildAutofac();
    [Benchmark] public object DryIoc() => Adapters.BuildDryIoc();
    [Benchmark] public object Jab() => SourceGenAdapters.BuildJab();
    [Benchmark] public object PureDI() => SourceGenAdapters.BuildPure();
    [Benchmark] public object StrongInject() => SourceGenAdapters.BuildStrongInject();
}

// Same axes, but over the concrete-type graph (no interfaces) — Stiletto binds
// via pure constructor-injection bindings with no [Provides] indirection.
[MemoryDiagnoser]
public class WarmResolveConcrete
{
    private global::Stiletto.Container _stiletto = null!;
    private ServiceProvider _msdi = null!;
    private global::Autofac.IContainer _autofac = null!;
    private global::DryIoc.Container _dryioc = null!;
    private JabConcrete _jab = null!;
    private PureConcrete _pure = null!;
    private SIConcrete _si = null!;

    [GlobalSetup]
    public void Setup()
    {
        _stiletto = ConcreteAdapters.BuildStiletto();
        _msdi = ConcreteAdapters.BuildMsdi();
        _autofac = ConcreteAdapters.BuildAutofac();
        _dryioc = ConcreteAdapters.BuildDryIoc();
        _jab = ConcreteSourceGenAdapters.BuildJab();
        _pure = ConcreteSourceGenAdapters.BuildPure();
        _si = ConcreteSourceGenAdapters.BuildStrongInject();

        _ = _stiletto.Get<CRoot>();
        _ = _msdi.GetRequiredService<CRoot>();
        _ = _autofac.Resolve<CRoot>();
        _ = _dryioc.Resolve<CRoot>();
        _ = ConcreteSourceGenAdapters.ResolveJab(_jab);
        _ = ConcreteSourceGenAdapters.ResolvePure(_pure);
        _ = ConcreteSourceGenAdapters.ResolveStrongInject(_si);
    }

    [Benchmark(Baseline = true)] public CRoot Stiletto() => _stiletto.Get<CRoot>();
    [Benchmark] public CRoot MicrosoftDI() => _msdi.GetRequiredService<CRoot>();
    [Benchmark] public CRoot Autofac() => _autofac.Resolve<CRoot>();
    [Benchmark] public CRoot DryIoc() => _dryioc.Resolve<CRoot>();
    [Benchmark] public CRoot Jab() => ConcreteSourceGenAdapters.ResolveJab(_jab);
    [Benchmark] public CRoot PureDI() => ConcreteSourceGenAdapters.ResolvePure(_pure);
    [Benchmark] public CRoot StrongInject() => ConcreteSourceGenAdapters.ResolveStrongInject(_si);
}

[MemoryDiagnoser]
public class ColdStartConcrete
{
    [Benchmark(Baseline = true)] public CRoot Stiletto() => ConcreteAdapters.BuildStiletto().Get<CRoot>();
    [Benchmark] public CRoot MicrosoftDI() => ConcreteAdapters.BuildMsdi().GetRequiredService<CRoot>();
    [Benchmark] public CRoot Autofac() => ConcreteAdapters.BuildAutofac().Resolve<CRoot>();
    [Benchmark] public CRoot DryIoc() => ConcreteAdapters.BuildDryIoc().Resolve<CRoot>();
    [Benchmark] public CRoot Jab() => ConcreteSourceGenAdapters.ResolveJab(ConcreteSourceGenAdapters.BuildJab());
    [Benchmark] public CRoot PureDI() => ConcreteSourceGenAdapters.ResolvePure(ConcreteSourceGenAdapters.BuildPure());
    [Benchmark] public CRoot StrongInject() => ConcreteSourceGenAdapters.ResolveStrongInject(ConcreteSourceGenAdapters.BuildStrongInject());
}
