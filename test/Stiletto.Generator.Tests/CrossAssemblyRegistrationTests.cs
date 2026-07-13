using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stiletto.Generator;
using Xunit;

namespace Stiletto.Generator.Tests
{
    /// <summary>
    /// Executable spec for the eager cross-assembly loader registration design
    /// (docs/design/cross-assembly-loader-registration.md).
    ///
    /// Reproduces the real production shape hermetically: a <b>producer</b> assembly
    /// owns an injectable (with a compiled binding + self-registering loader) and is
    /// <i>never touched</i>; a <b>consumer</b> assembly owns the module AND the
    /// <c>Container.Create</c> call — i.e. it is the anchor whose module initializer
    /// must eagerly register the producer's loader before the container snapshots the
    /// registry. Runs in registry-only mode (reflection fallback off) so a missed
    /// loader is a hard failure rather than a silent reflection fallback.
    ///
    /// EXPECTED TO FAIL until the generator emits the aggregate registrar: today the
    /// producer's <c>[ModuleInitializer]</c> never fires (nothing touches it), so its
    /// loader is absent from the consumer's frozen snapshot and resolution errors out.
    ///
    /// This test mutates the process-global <c>Stiletto.ReflectionFallback</c> switch;
    /// the assembly disables test parallelization (see TestParallelization.cs) so that
    /// and the shared <c>LoaderRegistry</c> stay sound.
    /// </summary>
    public class CrossAssemblyRegistrationTests
    {
        private const string ProducerSource = """
            using Stiletto;

            namespace Producer
            {
                // The injectable lives here, with its own compiled binding + loader.
                public class Service
                {
                    [Inject] public Service() { }
                }
            }
            """;

        // The module AND the Container.Create call live together in the consumer, so
        // the consumer is the anchor. It references the producer only through the
        // module's [Module(Injects = typeof(Producer.Service))] attribute — metadata,
        // never an executed token — so nothing in the consumer's own code path touches
        // the producer assembly at runtime.
        private const string ConsumerSource = """
            using Stiletto;

            namespace Consumer
            {
                [Module(Injects = new[] { typeof(global::Producer.Service) })]
                public class ServiceModule { }

                public static class Entry
                {
                    // Anchored here: registry-only resolution of a binding that lives
                    // in the (untouched) producer assembly.
                    public static void Run()
                        => Container.Create(typeof(ServiceModule)).Validate();
                }
            }
            """;

        [Fact]
        public void CreateAnchorRegistersUntouchedProducerAssembly()
        {
            var previous = ReadReflectionFallbackSwitch();
            AppContext.SetSwitch("Stiletto.ReflectionFallback", false); // registry-only

            var producerBytes = Compile("XAsmProducer", ProducerSource);
            var consumerBytes = Compile(
                "XAsmConsumer",
                ConsumerSource,
                extra: MetadataReference.CreateFromImage(producerBytes));

            // Load both from bytes. Neither module initializer has fired yet: loading an
            // assembly does NOT run it (proven in ModuleInitializerTimingTests).
            var producer = Assembly.Load(producerBytes);
            var consumer = Assembly.Load(consumerBytes);

            Assembly? Resolve(object? _, ResolveEventArgs e)
            {
                var name = new AssemblyName(e.Name).Name;
                if (name == producer.GetName().Name) return producer;
                if (name == consumer.GetName().Name) return consumer;
                return null;
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                // "Touch" the consumer (the anchor), firing its module initializer — but
                // deliberately NOT the producer. Post-fix, the consumer's aggregate
                // registrar runs here and registers the producer's loader; pre-fix, only
                // the consumer's own loader registers.
                RuntimeHelpers.RunModuleConstructor(consumer.ManifestModule.ModuleHandle);

                // The producer's loader must NOT have registered on its own — that is the
                // whole point (it was never touched).
                Assert.DoesNotContain(
                    Stiletto.LoaderRegistry.Snapshot(),
                    l => l.GetType().Assembly == producer);

                // Invoke the anchor's Create+Validate. Pre-fix this throws because the
                // producer's compiled binding is unreachable; post-fix it succeeds.
                var run = consumer.GetType("Consumer.Entry")!.GetMethod("Run")!;
                var error = Record.Exception(() => run.Invoke(null, null));

                Assert.True(
                    error is null,
                    "Registry-only resolution of a binding in an untouched producer "
                    + "assembly should succeed once the consumer (the Container.Create "
                    + "anchor) eagerly registers it, but resolution failed:\n"
                    + (error as TargetInvocationException)?.InnerException);

                // And the producer's loader is now present — registered eagerly by the
                // anchor, not by an incidental touch.
                Assert.Contains(
                    Stiletto.LoaderRegistry.Snapshot(),
                    l => l.GetType().Assembly == producer && l.GetType().Name == "CompiledLoader");
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                RestoreReflectionFallbackSwitch(previous);
            }
        }

        private static bool? ReadReflectionFallbackSwitch()
            => AppContext.TryGetSwitch("Stiletto.ReflectionFallback", out var v) ? v : (bool?)null;

        private static void RestoreReflectionFallbackSwitch(bool? previous)
        {
            if (previous is bool b)
                AppContext.SetSwitch("Stiletto.ReflectionFallback", b);
            else
                // No public API to clear a switch; restoring the default (true) is
                // equivalent to "unset" for ReflectionFallbackEnabled's read.
                AppContext.SetSwitch("Stiletto.ReflectionFallback", true);
        }

        private static byte[] Compile(string assemblyName, string source, params MetadataReference[] extra)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Append(typeof(Stiletto.InjectAttribute).Assembly.Location)
                .Distinct()
                .Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc))
                .Concat(extra)
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            CSharpGeneratorDriver.Create(new StilettoGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

            using var ms = new MemoryStream();
            var result = updated.Emit(ms);
            Assert.True(
                result.Success,
                "Emit failed:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            return ms.ToArray();
        }
    }
}
