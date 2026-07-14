using Stiletto;

namespace Stiletto.Benchmarks.Concrete;

// The same graph shape as Scenario.cs, but with no interfaces — every dependency
// is a concrete type. This lets Stiletto bind everything through generated
// constructor-injection bindings (its lightest path), with no [Provides]
// provider-method indirection, isolating construction cost from interface
// binding. The other containers register each concrete type as itself.
// [Singleton] is honored by Stiletto; the other adapters set singleton lifetime
// explicitly.

[Singleton] public sealed class CS1 { [Inject] public CS1() { } }
[Singleton] public sealed class CS2 { [Inject] public CS2() { } }
[Singleton] public sealed class CS3 { [Inject] public CS3() { } }

public sealed class CT1 { [Inject] public CT1() => Counters.Transients++; }
public sealed class CT2 { [Inject] public CT2() => Counters.Transients++; }
public sealed class CT3 { [Inject] public CT3() => Counters.Transients++; }

public sealed class CServiceA
{
    public CS1 S1 { get; }
    public CT1 T1 { get; }
    [Inject] public CServiceA(CS1 s1, CS2 s2, CS3 s3, CT1 t1, CT2 t2, CT3 t3) { S1 = s1; T1 = t1; }
}

public sealed class CServiceB
{
    public CS1 S1 { get; }
    public CT1 T1 { get; }
    [Inject] public CServiceB(CS1 s1, CS2 s2, CS3 s3, CT1 t1, CT2 t2, CT3 t3) { S1 = s1; T1 = t1; }
}

public sealed class CServiceC
{
    public CS1 S1 { get; }
    public CT1 T1 { get; }
    [Inject] public CServiceC(CS1 s1, CS2 s2, CS3 s3, CT1 t1, CT2 t2, CT3 t3) { S1 = s1; T1 = t1; }
}

public sealed class CRoot
{
    public CServiceA A { get; }
    public CServiceB B { get; }
    public CServiceC C { get; }
    [Inject] public CRoot(CServiceA a, CServiceB b, CServiceC c, CS1 s1, CT1 t1) { A = a; B = b; C = c; }
}
