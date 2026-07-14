using Jab;
using Pure.DI;
using StrongInject;

namespace Stiletto.Benchmarks;

// Jab: a partial provider class annotated with the graph; the generator emits
// GetService<T>. Built by `new`, resolved via GetService.
[ServiceProvider]
[Singleton<ISingleton1, Singleton1>]
[Singleton<ISingleton2, Singleton2>]
[Singleton<ISingleton3, Singleton3>]
[Transient<ITransient1, Transient1>]
[Transient<ITransient2, Transient2>]
[Transient<ITransient3, Transient3>]
[Transient<IServiceA, ServiceA>]
[Transient<IServiceB, ServiceB>]
[Transient<IServiceC, ServiceC>]
[Transient<ComplexRoot>]
public partial class JabProvider { }

// Pure.DI: the DI.Setup chain is read by the generator (the method body is not
// executed); it emits a Root property and constructor.
public partial class PureComposition
{
    private static void Setup() =>
        DI.Setup(nameof(PureComposition))
            .Bind<ISingleton1>().As(Lifetime.Singleton).To<Singleton1>()
            .Bind<ISingleton2>().As(Lifetime.Singleton).To<Singleton2>()
            .Bind<ISingleton3>().As(Lifetime.Singleton).To<Singleton3>()
            .Bind<ITransient1>().To<Transient1>()
            .Bind<ITransient2>().To<Transient2>()
            .Bind<ITransient3>().To<Transient3>()
            .Bind<IServiceA>().To<ServiceA>()
            .Bind<IServiceB>().To<ServiceB>()
            .Bind<IServiceC>().To<ServiceC>()
            .Root<ComplexRoot>("Root");
}

// StrongInject: attributes register the graph; the container implements
// IContainer<T> and the generator emits Resolve(), returning an Owned<T>.
// Scope.InstancePerDependency = a fresh instance per injection site (true
// transient). StrongInject's default is InstancePerResolution (shared within one
// Resolve), which would build fewer transients than the other containers.
[Register(typeof(Singleton1), Scope.SingleInstance, typeof(ISingleton1))]
[Register(typeof(Singleton2), Scope.SingleInstance, typeof(ISingleton2))]
[Register(typeof(Singleton3), Scope.SingleInstance, typeof(ISingleton3))]
[Register(typeof(Transient1), Scope.InstancePerDependency, typeof(ITransient1))]
[Register(typeof(Transient2), Scope.InstancePerDependency, typeof(ITransient2))]
[Register(typeof(Transient3), Scope.InstancePerDependency, typeof(ITransient3))]
[Register(typeof(ServiceA), Scope.InstancePerDependency, typeof(IServiceA))]
[Register(typeof(ServiceB), Scope.InstancePerDependency, typeof(IServiceB))]
[Register(typeof(ServiceC), Scope.InstancePerDependency, typeof(IServiceC))]
[Register(typeof(ComplexRoot), Scope.InstancePerDependency)]
public partial class StrongInjectContainer : IContainer<ComplexRoot> { }

public static class SourceGenAdapters
{
    public static JabProvider BuildJab() => new();
    public static ComplexRoot ResolveJab(JabProvider p) => p.GetService<ComplexRoot>();

    public static PureComposition BuildPure() => new();
    public static ComplexRoot ResolvePure(PureComposition c) => c.Root;

    public static StrongInjectContainer BuildStrongInject() => new();
    public static ComplexRoot ResolveStrongInject(StrongInjectContainer c)
    {
        using var owned = c.Resolve();
        return owned.Value;
    }
}
