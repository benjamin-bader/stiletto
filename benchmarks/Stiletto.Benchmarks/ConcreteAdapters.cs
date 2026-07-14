using Autofac;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Stiletto;
using Stiletto.Benchmarks.Concrete;

namespace Stiletto.Benchmarks;

// Stiletto binds everything through constructor injection — no [Provides] needed;
// concrete dependency types get generated inject bindings, [Singleton] honored.
[Module(Injects = new[] { typeof(CRoot) })]
public class StilettoConcreteModule { }

public static class ConcreteAdapters
{
    public static Stiletto.Container BuildStiletto()
        => Stiletto.Container.Create(typeof(StilettoConcreteModule));

    public static ServiceProvider BuildMsdi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CS1>();
        services.AddSingleton<CS2>();
        services.AddSingleton<CS3>();
        services.AddTransient<CT1>();
        services.AddTransient<CT2>();
        services.AddTransient<CT3>();
        services.AddTransient<CServiceA>();
        services.AddTransient<CServiceB>();
        services.AddTransient<CServiceC>();
        services.AddTransient<CRoot>();
        return services.BuildServiceProvider();
    }

    public static Autofac.IContainer BuildAutofac()
    {
        var b = new ContainerBuilder();
        b.RegisterType<CS1>().SingleInstance();
        b.RegisterType<CS2>().SingleInstance();
        b.RegisterType<CS3>().SingleInstance();
        b.RegisterType<CT1>();
        b.RegisterType<CT2>();
        b.RegisterType<CT3>();
        b.RegisterType<CServiceA>();
        b.RegisterType<CServiceB>();
        b.RegisterType<CServiceC>();
        b.RegisterType<CRoot>();
        return b.Build();
    }

    public static DryIoc.Container BuildDryIoc()
    {
        var c = new DryIoc.Container();
        c.Register<CS1>(DryIoc.Reuse.Singleton);
        c.Register<CS2>(DryIoc.Reuse.Singleton);
        c.Register<CS3>(DryIoc.Reuse.Singleton);
        c.Register<CT1>();
        c.Register<CT2>();
        c.Register<CT3>();
        c.Register<CServiceA>();
        c.Register<CServiceB>();
        c.Register<CServiceC>();
        c.Register<CRoot>();
        return c;
    }
}
