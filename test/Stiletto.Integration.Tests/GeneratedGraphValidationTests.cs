using Stiletto;
using Xunit;

namespace Stiletto.Integration.Tests
{
    /// <summary>
    /// Ports the old build-failure integration tests (CircularDependenciesFail,
    /// UnusedBindingsFail, DuplicateInjectsTypesFail). Under the Fody weaver those
    /// were compile-time errors; the source generator instead always emits bindings
    /// and leaves graph validation to the runtime. These types are compiled by the
    /// generator in this assembly, so the tests prove the *generated* bindings feed
    /// the runtime cycle/orphan/duplicate checks exactly as the woven ones did.
    /// </summary>
    public class GeneratedGraphValidationTests
    {
        [Fact]
        public void CircularDependencies_AreDetected_AtValidation()
        {
            Assert.Throws<InvalidOperationException>(
                () => Container.Create(typeof(CircularModule)).Validate());
        }

        [Fact]
        public void UnusedProviderMethods_AreDetected_AtValidation()
        {
            Assert.Throws<InvalidOperationException>(
                () => Container.Create(typeof(UnusedBindingsModule)).Validate());
        }

        [Fact]
        public void DuplicateInjectTypes_AreRejected_WhenBuildingTheContainer()
        {
            Assert.Throws<ArgumentException>(
                () => Container.Create(typeof(DuplicateInjectsModule)));
        }

        // --- Circular: Foo <-> Bar (property injection) plus a ctor that needs both. ---

        public class Foo
        {
            [Inject] public Bar Bar { get; set; }
        }

        public class Bar
        {
            [Inject] public Foo Foo { get; set; }
        }

        public class Foobar
        {
            [Inject]
            public Foobar(Foo foo, Bar bar)
            {
            }
        }

        [Module(Injects = new[] { typeof(Foobar) })]
        public class CircularModule
        {
        }

        // --- Unused: ProvideObject is never depended upon by the injected graph. ---

        public class NeedsAString
        {
            [Inject] public string Foo { get; set; }
        }

        [Module(Injects = new[] { typeof(NeedsAString) })]
        public class UnusedBindingsModule
        {
            [Provides] public string ProvideString() => "foo";
            [Provides] public object ProvideObject() => new object();
        }

        // --- Duplicate: the same type listed twice in a module's Injects. ---

        public class InjectableClass
        {
            [Inject] public string Foo { get; set; }
        }

        [Module(Injects = new[] { typeof(InjectableClass), typeof(InjectableClass) })]
        public class DuplicateInjectsModule
        {
            [Provides] public string ProvideString() => "foo";
        }
    }
}
