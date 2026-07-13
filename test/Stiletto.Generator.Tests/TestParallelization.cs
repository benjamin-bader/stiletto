// Several tests in this assembly mutate process-global state — the shared
// Stiletto.LoaderRegistry, the AppDomain AssemblyResolve event, module-initializer
// timing, and the Stiletto.ReflectionFallback AppContext switch. Running them
// concurrently would let one test observe another's global mutations, so disable
// test parallelization for the whole assembly.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
