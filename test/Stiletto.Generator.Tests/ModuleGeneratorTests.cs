using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stiletto.Generator;
using Xunit;
using static VerifyXunit.Verifier;

namespace Stiletto.Generator.Tests
{
    public class ModuleGeneratorTests
    {
        [Fact]
        public Task ModuleWithProviderMethods()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Heater { }
                    public class Pump { }

                    [Module(Injects = new[] { typeof(Heater) }, IsLibrary = true)]
                    public class DripCoffeeModule
                    {
                        [Provides]
                        public Heater ProvideHeater() { return new Heater(); }

                        [Provides, Named("main")]
                        public Pump ProvidePump(Heater heater) { return new Pump(); }

                        [Provides, Singleton]
                        public string Brand() { return "Stiletto"; }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task IncludedModulesAndEmptyModule()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    [Module(IncludedModules = new[] { typeof(OtherModule) })]
                    public class AggregateModule { }

                    [Module]
                    public class OtherModule { }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task SetProvidersContributeToSet()
        {
            // Two ProvidesType.Set methods contribute to the same ISet<string> via
            // SetBindings.Add<T>(bindings, setKey, ...).
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    [Module]
                    public class LogModule
                    {
                        [Provides(ProvidesType.Set)]
                        public string ConsoleSink() { return "console"; }

                        [Provides(ProvidesType.Set)]
                        public string FileSink() { return "file"; }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        private static GeneratorDriver RunGenerator(string source)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .Append(MetadataReference.CreateFromFile(typeof(Stiletto.InjectAttribute).Assembly.Location))
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName: "ModuleGeneratorTestAssembly",
                syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return CSharpGeneratorDriver.Create(new StilettoGenerator()).RunGenerators(compilation);
        }
    }
}
