namespace Stiletto.Generator.Tests
{
    /// <summary>
    /// Observation point for <see cref="ModuleInitializerTimingTests"/>. Lives in the
    /// (already-loaded) test assembly so a dynamically-loaded probe assembly can call
    /// into it from its module initializer, and the test can read the result without
    /// itself touching the probe assembly (which would perturb the timing being measured).
    /// </summary>
    public static class ProbeRegistry
    {
        public static int Count;

        public static void Mark() => Count++;

        public static void Reset() => Count = 0;
    }
}
