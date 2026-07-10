using Xunit;

namespace Stiletto.Tests
{
    public class ReflectionFallbackTests
    {
        [Fact]
        public void DefaultsToEnabled()
        {
            // Unless the Stiletto.ReflectionFallback switch is set off, reflection
            // remains available — so behavior stays a superset of the pre-P3 default.
            // (The switch-off path is proven end-to-end by the NativeAOT smoke test.)
            Assert.True(Container.ReflectionFallbackEnabled);
        }
    }
}
