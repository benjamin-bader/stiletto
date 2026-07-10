using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stiletto.Generator;
using Xunit;

namespace Stiletto.Generator.Tests
{
    /// <summary>
    /// The end-to-end proof: run the generator, compile its output into a real
    /// assembly, load it, and resolve through a live <c>Stiletto.Container</c>.
    /// Exercises all four increments at once — constructor injection (+ transitive),
    /// property injection, base-class chaining, a qualifier, and a singleton — and
    /// asserts the COMPILED binding is what the container uses, not reflection.
    /// </summary>
    public class ContainerIntegrationTests
    {
        private const string Source = """
            using System.Collections.Generic;
            using Stiletto;

            namespace IntegrationSample
            {
                public class Water { [Inject] public Water() { } }

                [Singleton]
                public class Heater { [Inject] public Heater() { } }

                public class Pump
                {
                    public Water Water;
                    [Inject] public Pump(Water water) { Water = water; }
                }

                public class ApplianceBase
                {
                    [Inject] public Heater Heater { get; set; }
                }

                public class CoffeeMaker : ApplianceBase
                {
                    public Pump Pump;
                    [Inject] public CoffeeMaker(Pump pump) { Pump = pump; }
                    [Inject, Named("brand")] public string Brand { get; set; }
                }

                [Module(Injects = new[] { typeof(CoffeeMaker) })]
                public class CoffeeModule
                {
                    [Provides, Named("brand")] public string Brand() { return "Stiletto"; }
                }
            }
            """;

        [Fact]
        public void CompiledBindingsResolveThroughContainer()
        {
            var assembly = CompileAndLoad(Source, "StilettoIntegrationSample");

            // (1) Both the compiled inject binding AND the compiled module were emitted.
            Assert.NotNull(assembly.GetType("IntegrationSample.CoffeeMaker_CompiledBinding"));
            Assert.NotNull(assembly.GetType("IntegrationSample.CoffeeModule_CompiledModule"));

            // (2) The loader the container consults BEFORE reflection returns exactly them.
            var codegen = new Stiletto.Internal.Loaders.Codegen.CodegenLoader();

            var binding = codegen.GetInjectBinding("IntegrationSample.CoffeeMaker", "IntegrationSample.CoffeeMaker", true);
            Assert.NotNull(binding);
            Assert.Equal("CoffeeMaker_CompiledBinding", binding!.GetType().Name);

            var runtimeModule = codegen.GetRuntimeModule(assembly.GetType("IntegrationSample.CoffeeModule")!, null);
            Assert.NotNull(runtimeModule);
            Assert.Equal("CoffeeModule_CompiledModule", runtimeModule!.GetType().Name);

            // (3) Full resolution through a real Container yields a correctly wired graph.
            var moduleType = assembly.GetType("IntegrationSample.CoffeeModule")!;
            var container = Stiletto.Container.Create(moduleType);

            var coffeeMakerType = assembly.GetType("IntegrationSample.CoffeeMaker")!;
            var get = typeof(Stiletto.Container).GetMethod("Get")!.MakeGenericMethod(coffeeMakerType);

            var cm1 = get.Invoke(container, null)!;
            var cm2 = get.Invoke(container, null)!;

            // Constructor injection, plus a transitive dependency (Pump -> Water).
            var pump = coffeeMakerType.GetField("Pump")!.GetValue(cm1)!;
            Assert.NotNull(pump);
            Assert.NotNull(pump.GetType().GetField("Water")!.GetValue(pump));

            // Base-class property injection.
            var heater1 = coffeeMakerType.GetProperty("Heater")!.GetValue(cm1);
            Assert.NotNull(heater1);

            // Named property injection, satisfied by the (reflection) module's [Provides].
            Assert.Equal("Stiletto", coffeeMakerType.GetProperty("Brand")!.GetValue(cm1));

            // [Singleton] honored across two independent resolutions.
            var heater2 = coffeeMakerType.GetProperty("Heater")!.GetValue(cm2);
            Assert.Same(heater1, heater2);
        }

        private const string SetSource = """
            using System.Collections.Generic;
            using Stiletto;

            namespace SetSample
            {
                public class Sinks
                {
                    public ISet<string> All;
                    [Inject] public Sinks(ISet<string> all) { All = all; }
                }

                [Module(Injects = new[] { typeof(Sinks) })]
                public class LogModule
                {
                    [Provides(ProvidesType.Set)] public string ConsoleSink() { return "console"; }
                    [Provides(ProvidesType.Set)] public string FileSink() { return "file"; }
                }
            }
            """;

        [Fact]
        public void SetBindingsResolveThroughContainer()
        {
            // Two [Provides(Set)] methods contribute to an ISet<string> that a
            // compiled inject binding consumes as a constructor dependency — fully
            // compiled, end-to-end.
            var assembly = CompileAndLoad(SetSource, "StilettoSetSample");

            var moduleType = assembly.GetType("SetSample.LogModule")!;
            var container = Stiletto.Container.Create(moduleType);

            var sinksType = assembly.GetType("SetSample.Sinks")!;
            var get = typeof(Stiletto.Container).GetMethod("Get")!.MakeGenericMethod(sinksType);
            var sinks = get.Invoke(container, null)!;

            var all = (IEnumerable<string>)sinksType.GetField("All")!.GetValue(sinks)!;

            Assert.Equal(new[] { "console", "file" }, all.OrderBy(s => s, StringComparer.Ordinal));
        }

        private const string RegSource = """
            using Stiletto;

            namespace RegSample
            {
                public class Widget { [Inject] public Widget() { } }

                [Module(Injects = new[] { typeof(Widget) })]
                public class WidgetModule { }
            }
            """;

        [Fact]
        public void CompiledLoaderRegistersViaModuleInitializer()
        {
            var assembly = CompileAndLoad(RegSource, "StilettoRegSample");

            // Simulate the client's compiled `typeof(Module)` — an ldtoken that fires
            // this assembly's [ModuleInitializer], which registers the generated loader.
            // (Proven in ModuleInitializerTimingTests; here we run it explicitly since
            // the test reaches types reflectively rather than via a compiled typeof.)
            System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

            // The generated loader registered itself — no AppDomain scan involved.
            var registered = Stiletto.LoaderRegistry.Snapshot();
            Assert.Contains(registered, l => l.GetType().Assembly == assembly && l.GetType().Name == "CompiledLoader");

            // And the container resolves through the registered compiled loader.
            var container = Stiletto.Container.Create(assembly.GetType("RegSample.WidgetModule")!);
            var widgetType = assembly.GetType("RegSample.Widget")!;
            var get = typeof(Stiletto.Container).GetMethod("Get")!.MakeGenericMethod(widgetType);
            Assert.NotNull(get.Invoke(container, null));
        }

        private static Assembly CompileAndLoad(string source, string assemblyName)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Append(typeof(Stiletto.InjectAttribute).Assembly.Location)
                .Distinct()
                .Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc))
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName: assemblyName,
                syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            CSharpGeneratorDriver.Create(new StilettoGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

            using var ms = new MemoryStream();
            var result = updated.Emit(ms);
            Assert.True(
                result.Success,
                "Emit failed:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            return Assembly.Load(ms.ToArray());
        }
    }
}
