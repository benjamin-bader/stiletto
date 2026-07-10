using System.Runtime.CompilerServices;

namespace Stiletto.Generator.Tests
{
    public static class ModuleInit
    {
        [ModuleInitializer]
        public static void Init()
        {
            // Teaches Verify how to serialize GeneratorDriver run results.
            VerifySourceGenerators.Initialize();
        }
    }
}
