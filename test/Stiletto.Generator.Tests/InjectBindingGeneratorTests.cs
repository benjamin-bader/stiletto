using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stiletto.Generator;
using Xunit;
using static VerifyXunit.Verifier;

namespace Stiletto.Generator.Tests
{
    public class InjectBindingGeneratorTests
    {
        [Fact]
        public Task ParameterlessInjectConstructor()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Widget
                    {
                        [Inject]
                        public Widget() { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task SingleConstructorParameter()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Pump { [Inject] public Pump() { } }

                    public class CoffeeMaker
                    {
                        [Inject]
                        public CoffeeMaker(Pump pump) { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task MultipleConstructorParameters()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Heater { [Inject] public Heater() { } }
                    public class Pump { [Inject] public Pump() { } }

                    public class CoffeeMaker
                    {
                        [Inject]
                        public CoffeeMaker(Heater heater, Pump pump) { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task NamedConstructorParameter()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Connection
                    {
                        [Inject]
                        public Connection([Named("primary")] Endpoint endpoint) { }
                    }

                    public class Endpoint { [Inject] public Endpoint() { } }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task SingletonType()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    [Singleton]
                    public class Cache
                    {
                        [Inject]
                        public Cache() { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task TypeInGlobalNamespace()
        {
            const string source = """
                using Stiletto;

                public class Rootless
                {
                    [Inject]
                    public Rootless() { }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task PropertyOnlyInjection()
        {
            // No [Inject] ctor: the default constructor is used, and the property
            // is injected via the InjectProperties override.
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Dep { [Inject] public Dep() { } }

                    public class NeedsProperty
                    {
                        [Inject]
                        public Dep Dependency { get; set; }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task ConstructorAndPropertyInjection()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Heater { [Inject] public Heater() { } }
                    public class Pump { [Inject] public Pump() { } }

                    public class CoffeeMaker
                    {
                        [Inject]
                        public CoffeeMaker(Heater heater) { }

                        [Inject]
                        public Pump Pump { get; set; }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task NamedPropertyInjection()
        {
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Endpoint { [Inject] public Endpoint() { } }

                    public class Service
                    {
                        [Inject, Named("primary")]
                        public Endpoint Endpoint { get; set; }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task BaseClassWithInjectMembersIsChained()
        {
            // BaseInjectable has an [Inject] property; DerivedInjectable must emit a
            // baseTypeBinding (members/BaseInjectable) and chain InjectProperties.
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class Dude { [Inject] public Dude() { } }

                    public class BaseInjectable
                    {
                        [Inject]
                        public Dude TheDude { get; set; }
                    }

                    public class DerivedInjectable : BaseInjectable
                    {
                        [Inject]
                        public DerivedInjectable() { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public Task NonInjectableBaseIsNotChained()
        {
            // BaseThing has no inject members, so Derived must NOT emit a
            // baseTypeBinding (mirrors ReflectionInjectBinding).
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class BaseThing { }

                    public class Derived : BaseThing
                    {
                        [Inject]
                        public Derived() { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        [Fact]
        public void SkipsGenericBaseWithInjectMembers()
        {
            // A closed-generic base with inject members can't be keyed in v1
            // (backtick-arity), so the derived type falls back to reflection.
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    public class GenericBase<T>
                    {
                        [Inject]
                        public string Name { get; set; }
                    }

                    public class Derived : GenericBase<int>
                    {
                        [Inject]
                        public Derived() { }
                    }
                }
                """;

            var driver = RunGenerator(source);
            var sources = driver.GetRunResult().Results.Single().GeneratedSources;

            // GenericBase<T> is generic (skipped); Derived's base is generic-with-
            // inject-members (skipped). Nothing emitted.
            Assert.Empty(sources);
        }

        [Fact]
        public Task GenericDependencyIsInjected()
        {
            const string source = """
                using Stiletto;
                using System;
                using System.Collections.Generic;
                namespace Sample
                {
                    public class Bean { [Inject] public Bean() { } }

                    public class Roaster
                    {
                        [Inject]
                        public Roaster(IList<Bean> beans, Lazy<Bean> lazyBean, IProvider<Bean> beanProvider) { }
                    }
                }
                """;
            return Verify(RunGenerator(source));
        }

        public static IEnumerable<object[]> GenericDependencyTypes()
        {
            yield return [typeof(IList<string>)];
            yield return [typeof(Lazy<int>)];
            yield return [typeof(Stiletto.IProvider<string>)];
            yield return [typeof(IDictionary<string, int>)];
            yield return [typeof(IList<IList<string>>)];
        }

        [Theory]
        [MemberData(nameof(GenericDependencyTypes))]
        public void GenericKeyMatchesRuntimeKey(Type dependencyType)
        {
            // The generated key must equal what the runtime Stiletto.Key produces for
            // the same constructed generic, byte-for-byte (backtick arity included).
            var source = $$"""
                using Stiletto;
                namespace Sample
                {
                    public class Consumer
                    {
                        [Inject]
                        public Consumer({{CSharpName(dependencyType)}} dependency) { }
                    }
                }
                """;

            // Concatenate all generated sources (the binding plus the aggregated
            // loader); the RequestBinding call lives in the binding.
            var generated = string.Concat(RunGenerator(source).GetRunResult().Results
                .Single().GeneratedSources.Select(s => s.SourceText.ToString()));

            var expectedKey = Stiletto.Key.Get(dependencyType);
            Assert.Contains($"RequestBinding(\"{expectedKey}\"", generated);
        }

        private static string CSharpName(Type type)
        {
            if (!type.IsGenericType)
            {
                return "global::" + type.FullName!.Replace('+', '.');
            }

            var definition = type.GetGenericTypeDefinition().FullName!;
            var raw = "global::" + definition.Substring(0, definition.IndexOf('`')).Replace('+', '.');
            var args = string.Join(", ", type.GetGenericArguments().Select(CSharpName));
            return raw + "<" + args + ">";
        }

        [Fact]
        public void GeneratedBindingsCompileCleanly()
        {
            // A snapshot proves the text; this proves the emitted C# is valid code
            // that binds against the real Stiletto.Internal.Binding/Resolver types.
            const string source = """
                using Stiletto;
                namespace Sample
                {
                    [Singleton]
                    public class Heater { [Inject] public Heater() { } }
                    public class Pump { [Inject] public Pump() { } }

                    public class Grinder { [Inject] public Grinder() { } }

                    public class ApplianceBase
                    {
                        [Inject]
                        public Heater Heater { get; set; }
                    }

                    public class CoffeeMaker : ApplianceBase
                    {
                        [Inject]
                        public CoffeeMaker(Heater heater, [Named("main")] Pump pump) { }

                        [Inject]
                        public Grinder Grinder { get; set; }
                    }
                }
                """;

            var compilation = CreateCompilation(source);
            var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver
                .Create(new StilettoGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _, TestContext.Current.CancellationToken);

            // No errors from the generator run itself...
            Assert.Empty(driver.GetRunResult().Diagnostics);

            // ...and the generated trees compile with no errors against Stiletto.
            var errors = updated.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.Empty(errors);
        }

        private static GeneratorDriver RunGenerator(string source)
        {
            var generator = new StilettoGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            return driver.RunGenerators(CreateCompilation(source));
        }

        private static CSharpCompilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            // Reference the whole loaded framework plus Stiletto so the compilation
            // binds cleanly and [Inject]/[Named]/[Singleton] resolve to real symbols.
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .Append(MetadataReference.CreateFromFile(typeof(Stiletto.InjectAttribute).Assembly.Location))
                .ToList();

            return CSharpCompilation.Create(
                assemblyName: "GeneratorTestAssembly",
                syntaxTrees: [syntaxTree],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
