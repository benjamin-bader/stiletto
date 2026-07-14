using Stiletto;

namespace Stiletto.Benchmarks;

// A shared "complex" object graph, modeled on Daniel Palme's IocPerformance
// scenarios: three singletons and three transients, three services that each
// depend on all of them, and a root that pulls the services together. This is
// the capability intersection every container under test can express through
// plain constructor injection.
//
// The interfaces + impls are wired by each container's own adapter. Only
// ComplexRoot carries [Inject] — the other containers ignore the attribute and
// use its public constructor, while Stiletto binds it through generated code.

public interface ISingleton1 { }
public interface ISingleton2 { }
public interface ISingleton3 { }

public sealed class Singleton1 : ISingleton1 { }
public sealed class Singleton2 : ISingleton2 { }
public sealed class Singleton3 : ISingleton3 { }

public interface ITransient1 { }
public interface ITransient2 { }
public interface ITransient3 { }

// Counts constructions so a --verify pass can confirm every container builds the
// same number of transients (i.e. measures equivalent work).
public static class Counters { public static int Transients; }

public sealed class Transient1 : ITransient1 { public Transient1() => Counters.Transients++; }
public sealed class Transient2 : ITransient2 { public Transient2() => Counters.Transients++; }
public sealed class Transient3 : ITransient3 { public Transient3() => Counters.Transients++; }

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }

public sealed class ServiceA(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3) : IServiceA
{
    public ISingleton1 S1 { get; } = s1;
    public ITransient1 T1 { get; } = t1;
    public ITransient2 T2 { get; } = t2;
    public ITransient3 T3 { get; } = t3;
}

public sealed class ServiceB(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3) : IServiceB
{
    public ISingleton1 S1 { get; } = s1;
    public ITransient1 T1 { get; } = t1;
    public ITransient2 T2 { get; } = t2;
    public ITransient3 T3 { get; } = t3;
}

public sealed class ServiceC(ISingleton1 s1, ISingleton2 s2, ISingleton3 s3, ITransient1 t1, ITransient2 t2, ITransient3 t3) : IServiceC
{
    public ISingleton1 S1 { get; } = s1;
    public ITransient1 T1 { get; } = t1;
    public ITransient2 T2 { get; } = t2;
    public ITransient3 T3 { get; } = t3;
}

public sealed class ComplexRoot
{
    public IServiceA A { get; }
    public IServiceB B { get; }
    public IServiceC C { get; }

    [Inject]
    public ComplexRoot(IServiceA a, IServiceB b, IServiceC c, ISingleton1 s1, ITransient1 t1)
    {
        A = a;
        B = b;
        C = c;
    }
}
