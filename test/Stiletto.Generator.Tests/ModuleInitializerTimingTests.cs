using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Stiletto.Generator.Tests
{
    /// <summary>
    /// Validates the load-bearing assumption behind the planned [ModuleInitializer]-based
    /// loader registration: a consumer's compiled <c>typeof(TypeInAnotherAssembly)</c>
    /// (an <c>ldtoken</c> — exactly what <c>Container.Create(typeof(Module))</c> does)
    /// triggers that assembly's module initializer. Also confirms the negatives: merely
    /// loading the assembly (or a consumer that references it) does NOT fire it.
    /// </summary>
    public class ModuleInitializerTimingTests
    {
        [Fact]
        public void ConsumerTypeofFiresProducerModuleInitializer()
        {
            ProbeRegistry.Reset();

            // Producer: a module initializer that marks the shared registry, plus a type to touch.
            var producerBytes = Compile(
                assemblyName: "ProbeProducer",
                source: """
                    namespace Probe
                    {
                        public class Marker { }

                        internal static class Init
                        {
                            [System.Runtime.CompilerServices.ModuleInitializer]
                            internal static void Run() => global::Stiletto.Generator.Tests.ProbeRegistry.Mark();
                        }
                    }
                    """);

            var producer = Assembly.Load(producerBytes);

            // (a) Loading the producer assembly alone must NOT run its module initializer.
            Assert.Equal(0, ProbeRegistry.Count);

            // Consumer: compiled `typeof(Probe.Marker)` — the ldtoken the real client emits.
            var consumerBytes = Compile(
                assemblyName: "ProbeConsumer",
                source: """
                    namespace Consumer
                    {
                        public static class Entry
                        {
                            public static System.Type Touch() => typeof(global::Probe.Marker);
                        }
                    }
                    """,
                extra: MetadataReference.CreateFromImage(producerBytes));

            // Resolve the producer (loaded from bytes, so it has no location) by identity.
            Assembly Resolve(object? _, ResolveEventArgs e)
                => new AssemblyName(e.Name).Name == producer.GetName().Name ? producer : null!;

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                var consumer = Assembly.Load(consumerBytes);

                // (b) Loading a consumer that references the producer must NOT fire it either.
                Assert.Equal(0, ProbeRegistry.Count);

                // (c) Executing the consumer's `typeof(Probe.Marker)` DOES fire it — once.
                consumer.GetType("Consumer.Entry")!.GetMethod("Touch")!.Invoke(null, null);
                Assert.Equal(1, ProbeRegistry.Count);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            }
        }

        private static byte[] Compile(string assemblyName, string source, params MetadataReference[] extra)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .Concat(extra)
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            Assert.True(
                result.Success,
                "Emit failed:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            return ms.ToArray();
        }
    }
}
