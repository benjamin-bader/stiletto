using Autofac;
using BenchmarkDotNet.Running;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Stiletto.Benchmarks;

if (args.Length > 0 && args[0] == "verify")
{
    static void Check(string name, Func<ComplexRoot> resolve)
    {
        Counters.Transients = 0;
        var root = resolve();
        System.Console.WriteLine($"{name,-14} transients={Counters.Transients}  wired={root.A is not null && root.B is not null && root.C is not null}");
    }

    Check("Stiletto", () => Adapters.BuildStiletto().Get<ComplexRoot>());
    Check("MicrosoftDI", () => Adapters.BuildMsdi().GetRequiredService<ComplexRoot>());
    Check("Autofac", () => Adapters.BuildAutofac().Resolve<ComplexRoot>());
    Check("DryIoc", () => Adapters.BuildDryIoc().Resolve<ComplexRoot>());
    Check("Jab", () => SourceGenAdapters.ResolveJab(SourceGenAdapters.BuildJab()));
    Check("PureDI", () => SourceGenAdapters.ResolvePure(SourceGenAdapters.BuildPure()));
    Check("StrongInject", () => SourceGenAdapters.ResolveStrongInject(SourceGenAdapters.BuildStrongInject()));
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(WarmResolve).Assembly).Run(args);
