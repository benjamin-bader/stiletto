using Autofac;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Stiletto;

namespace Stiletto.Benchmarks;

// The Stiletto module binds each interface via a [Provides] method (singletons
// marked [Singleton]); ComplexRoot is bound by its [Inject] constructor.
[Module(Injects = new[] { typeof(ComplexRoot) })]
public class StilettoBenchModule
{
    [Provides, Singleton] public ISingleton1 S1() => new Singleton1();
    [Provides, Singleton] public ISingleton2 S2() => new Singleton2();
    [Provides, Singleton] public ISingleton3 S3() => new Singleton3();

    [Provides] public ITransient1 T1() => new Transient1();
    [Provides] public ITransient2 T2() => new Transient2();
    [Provides] public ITransient3 T3() => new Transient3();

    [Provides] public IServiceA A(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3)
        => new ServiceA(s1, s2, s3, t1, t2, t3);

    [Provides] public IServiceB B(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3)
        => new ServiceB(s1, s2, s3, t1, t2, t3);

    [Provides] public IServiceC C(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3)
        => new ServiceC(s1, s2, s3, t1, t2, t3);
}

public static class Adapters
{
    public static Stiletto.Container BuildStiletto()
        => Stiletto.Container.Create(typeof(StilettoBenchModule));

    public static ServiceProvider BuildMsdi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingleton1, Singleton1>();
        services.AddSingleton<ISingleton2, Singleton2>();
        services.AddSingleton<ISingleton3, Singleton3>();
        services.AddTransient<ITransient1, Transient1>();
        services.AddTransient<ITransient2, Transient2>();
        services.AddTransient<ITransient3, Transient3>();
        services.AddTransient<IServiceA, ServiceA>();
        services.AddTransient<IServiceB, ServiceB>();
        services.AddTransient<IServiceC, ServiceC>();
        services.AddTransient<ComplexRoot>();
        return services.BuildServiceProvider();
    }

    public static Autofac.IContainer BuildAutofac()
    {
        var b = new ContainerBuilder();
        b.RegisterType<Singleton1>().As<ISingleton1>().SingleInstance();
        b.RegisterType<Singleton2>().As<ISingleton2>().SingleInstance();
        b.RegisterType<Singleton3>().As<ISingleton3>().SingleInstance();
        b.RegisterType<Transient1>().As<ITransient1>();
        b.RegisterType<Transient2>().As<ITransient2>();
        b.RegisterType<Transient3>().As<ITransient3>();
        b.RegisterType<ServiceA>().As<IServiceA>();
        b.RegisterType<ServiceB>().As<IServiceB>();
        b.RegisterType<ServiceC>().As<IServiceC>();
        b.RegisterType<ComplexRoot>().AsSelf();
        return b.Build();
    }

    public static DryIoc.Container BuildDryIoc()
    {
        var c = new DryIoc.Container();
        c.Register<ISingleton1, Singleton1>(DryIoc.Reuse.Singleton);
        c.Register<ISingleton2, Singleton2>(DryIoc.Reuse.Singleton);
        c.Register<ISingleton3, Singleton3>(DryIoc.Reuse.Singleton);
        c.Register<ITransient1, Transient1>();
        c.Register<ITransient2, Transient2>();
        c.Register<ITransient3, Transient3>();
        c.Register<IServiceA, ServiceA>();
        c.Register<IServiceB, ServiceB>();
        c.Register<IServiceC, ServiceC>();
        c.Register<ComplexRoot>();
        return c;
    }
}
