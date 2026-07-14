using Jab;
using Pure.DI;
using StrongInject;
using Stiletto.Benchmarks.Concrete;

namespace Stiletto.Benchmarks;

[ServiceProvider]
[Singleton<CS1>]
[Singleton<CS2>]
[Singleton<CS3>]
[Transient<CT1>]
[Transient<CT2>]
[Transient<CT3>]
[Transient<CServiceA>]
[Transient<CServiceB>]
[Transient<CServiceC>]
[Transient<CRoot>]
public partial class JabConcrete { }

public partial class PureConcrete
{
    private static void Setup() =>
        DI.Setup(nameof(PureConcrete))
            .Bind<CS1>().As(Lifetime.Singleton).To<CS1>()
            .Bind<CS2>().As(Lifetime.Singleton).To<CS2>()
            .Bind<CS3>().As(Lifetime.Singleton).To<CS3>()
            .Bind<CT1>().To<CT1>()
            .Bind<CT2>().To<CT2>()
            .Bind<CT3>().To<CT3>()
            .Bind<CServiceA>().To<CServiceA>()
            .Bind<CServiceB>().To<CServiceB>()
            .Bind<CServiceC>().To<CServiceC>()
            .Root<CRoot>("Root");
}

[Register(typeof(CS1), Scope.SingleInstance)]
[Register(typeof(CS2), Scope.SingleInstance)]
[Register(typeof(CS3), Scope.SingleInstance)]
[Register(typeof(CT1), Scope.InstancePerDependency)]
[Register(typeof(CT2), Scope.InstancePerDependency)]
[Register(typeof(CT3), Scope.InstancePerDependency)]
[Register(typeof(CServiceA), Scope.InstancePerDependency)]
[Register(typeof(CServiceB), Scope.InstancePerDependency)]
[Register(typeof(CServiceC), Scope.InstancePerDependency)]
[Register(typeof(CRoot), Scope.InstancePerDependency)]
public partial class SIConcrete : IContainer<CRoot> { }

public static class ConcreteSourceGenAdapters
{
    public static JabConcrete BuildJab() => new();
    public static CRoot ResolveJab(JabConcrete p) => p.GetService<CRoot>();

    public static PureConcrete BuildPure() => new();
    public static CRoot ResolvePure(PureConcrete c) => c.Root;

    public static SIConcrete BuildStrongInject() => new();
    public static CRoot ResolveStrongInject(SIConcrete c)
    {
        using var owned = c.Resolve();
        return owned.Value;
    }
}
