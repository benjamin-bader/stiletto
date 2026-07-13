using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Stiletto.Generator
{
    /// <summary>
    /// The single Stiletto source generator. Emits per-type inject bindings, per-module
    /// compiled modules, and — from the collected models — one aggregated
    /// <c>Stiletto.Generated.CompiledLoader</c> per assembly plus a
    /// <c>[ModuleInitializer]</c> that registers it with <c>Stiletto.LoaderRegistry</c>.
    /// The client experience is unchanged (<c>Container.Create(typeof(Module))</c>);
    /// registration is automatic and needs no <see cref="System.AppDomain"/> scanning.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class StilettoGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var injectModels = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    InjectBindingEmitter.InjectAttributeMetadataName,
                    predicate: static (node, _) => node is ConstructorDeclarationSyntax or PropertyDeclarationSyntax,
                    transform: static (ctx, _) => InjectBindingEmitter.BuildModel(ctx.TargetSymbol.ContainingType))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!)
                .Collect();

            var moduleModels = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ModuleEmitter.ModuleAttributeMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => ModuleEmitter.BuildModel((INamedTypeSymbol)ctx.TargetSymbol))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!)
                .Collect();

            context.RegisterSourceOutput(injectModels, static (spc, models) =>
            {
                // A type with both an [Inject] ctor and [Inject] properties fires
                // multiple times, producing identical models; emit each once.
                var seen = new HashSet<string>();
                foreach (var model in models)
                {
                    if (seen.Add(model.HintName))
                    {
                        spc.AddSource(model.HintName, InjectBindingEmitter.Emit(model));
                    }
                }
            });

            context.RegisterSourceOutput(moduleModels, static (spc, models) =>
            {
                var seen = new HashSet<string>();
                foreach (var model in models)
                {
                    if (seen.Add(model.HintName))
                    {
                        spc.AddSource(model.HintName, ModuleEmitter.Emit(model));
                    }
                }
            });

            // The assembly name feeds the unique, per-assembly public registrar type.
            // Selecting just the name keeps this cached (a bare CompilationProvider
            // would recompute the loader emit on every keystroke).
            var assemblyName = context.CompilationProvider
                .Select(static (c, _) => c.AssemblyName ?? "Stiletto");

            context.RegisterSourceOutput(injectModels.Combine(moduleModels).Combine(assemblyName),
                static (spc, data) => EmitLoader(spc, data.Left.Left, data.Left.Right, data.Right));

            // --- Eager aggregate registrar (see docs/design/cross-assembly-loader-registration.md) ---
            // Anchor: this assembly either calls Container.Create/CreateWithLoaders, or
            // is an executable. Either way it is a point where a container is (or may be)
            // built, so every compiled loader in its reference closure must be registered
            // first. Its own module initializer, running before its own code, guarantees
            // the aggregate fires before any Create call here.
            var callsCreate = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsMaybeCreateInvocation(node),
                    transform: static (ctx, _) => IsContainerCreateInvocation(ctx))
                .Where(static isCreate => isCreate)
                .Collect()
                .Select(static (calls, _) => !calls.IsEmpty);

            // The entry .exe kinds (Roslyn's own OutputKind.IsApplication set) — but not
            // a .winmd component, .netmodule, or class library. Hand-listed because
            // OutputKindExtensions.IsApplication is internal to Roslyn.
            var isExecutable = context.CompilationProvider
                .Select(static (c, _) => c.Options.OutputKind
                    is OutputKind.ConsoleApplication
                    or OutputKind.WindowsApplication
                    or OutputKind.WindowsRuntimeApplication);

            // Registration entry points advertised by referenced, compiled Stiletto
            // assemblies. Projected to a sorted, value-equatable array so downstream
            // output caches across edits that don't change references.
            var referencedRegistrars = context.CompilationProvider
                .Select(static (c, _) => CollectReferencedRegistrars(c));

            var anchor = callsCreate.Combine(isExecutable).Combine(referencedRegistrars);
            context.RegisterSourceOutput(anchor, static (spc, data) =>
                EmitAggregateRegistrar(spc, isAnchor: data.Left.Left || data.Left.Right, registrars: data.Right));
        }

        private static bool IsMaybeCreateInvocation(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                return false;
            }

            var name = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null,
            };

            return name is "Create" or "CreateWithLoaders";
        }

        private static bool IsContainerCreateInvocation(GeneratorSyntaxContext ctx)
        {
            if (ctx.SemanticModel.GetSymbolInfo((InvocationExpressionSyntax)ctx.Node).Symbol is not IMethodSymbol method)
            {
                return false;
            }

            return method.Name is "Create" or "CreateWithLoaders"
                && method.ContainingType?.ToDisplayString() == "Stiletto.Container";
        }

        private static EquatableArray<string> CollectReferencedRegistrars(Compilation compilation)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                foreach (var attribute in reference.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() == "Stiletto.StilettoLoaderAssemblyAttribute"
                        && attribute.ConstructorArguments.Length == 1
                        && attribute.ConstructorArguments[0].Value is string registrationTypeName)
                    {
                        names.Add(registrationTypeName);
                    }
                }
            }

            return new EquatableArray<string>(names.ToImmutableArray());
        }

        private static void EmitAggregateRegistrar(
            SourceProductionContext spc,
            bool isAnchor,
            EquatableArray<string> registrars)
        {
            if (!isAnchor || registrars.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Stiletto.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Stiletto.Generator\", null)]");
            sb.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    internal static class ReferencedLoaderRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        // Runs before any Container.Create in this assembly: eagerly registers");
            sb.AppendLine("        // every compiled loader in the reference closure so none is missed by a");
            sb.AppendLine("        // container's one-time registry snapshot.");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("        internal static void RegisterAll()");
            sb.AppendLine("        {");
            foreach (var registrar in registrars)
            {
                sb.Append("            global::").Append(registrar).AppendLine(".EnsureRegistered();");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("Stiletto.Generated.ReferencedLoaderRegistration.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void EmitLoader(
            SourceProductionContext spc,
            ImmutableArray<InjectBindingModel> injectModels,
            ImmutableArray<ModuleModel> moduleModels,
            string assemblyName)
        {
            if (injectModels.IsEmpty && moduleModels.IsEmpty)
            {
                // Nothing compiled in this assembly — no loader, no registration.
                return;
            }

            var registrarName = SanitizeIdentifier(assemblyName);
            var registrarFullName = "Stiletto.Generated.Registrations." + registrarName;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            // Marks this assembly, for the consuming aggregate, as one that carries a
            // compiled loader — advertising the public entry point to register it.
            sb.Append("[assembly: global::Stiletto.StilettoLoaderAssembly(")
              .Append(Literal(registrarFullName)).AppendLine(")]");
            sb.AppendLine("namespace Stiletto.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Stiletto.Generator\", null)]");
            sb.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    internal sealed class CompiledLoader : global::Stiletto.Internal.ILoader");
            sb.AppendLine("    {");

            // GetInjectBinding: className -> new {Type}_CompiledBinding()
            sb.AppendLine("        public global::Stiletto.Internal.Binding GetInjectBinding(string key, string className, bool mustBeInjectable)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (className)");
            sb.AppendLine("            {");
            var seenInject = new HashSet<string>();
            foreach (var model in injectModels)
            {
                if (seenInject.Add(model.Key))
                {
                    sb.Append("                case ").Append(Literal(model.Key)).Append(": return new ")
                      .Append(GlobalName(model.Namespace, model.BindingTypeName)).AppendLine("();");
                }
            }
            sb.AppendLine("                default: return null!;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetRuntimeModule: moduleType.FullName -> new {Module}_CompiledModule()
            sb.AppendLine("        public global::Stiletto.Internal.RuntimeModule GetRuntimeModule(global::System.Type moduleType, object? moduleInstance)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (moduleType.FullName)");
            sb.AppendLine("            {");
            var seenModule = new HashSet<string>();
            foreach (var model in moduleModels)
            {
                if (seenModule.Add(model.ReflectionName))
                {
                    sb.Append("                case ").Append(Literal(model.ReflectionName)).Append(": return new ")
                      .Append(GlobalName(model.Namespace, model.CompiledTypeName)).AppendLine("();");
                }
            }
            sb.AppendLine("                default: return null!;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Lazy<T> / IProvider<T> dependencies discovered anywhere in the assembly
            // become concrete LazyBinding<T> / ProviderBinding<T> instantiations.
            var lazyCases = new List<WrapperModel>();
            var providerCases = new List<WrapperModel>();
            var lazySeen = new HashSet<string>();
            var providerSeen = new HashSet<string>();
            foreach (var model in injectModels)
            {
                CollectWrappers(model.Wrappers, lazyCases, providerCases, lazySeen, providerSeen);
            }
            foreach (var model in moduleModels)
            {
                CollectWrappers(model.Wrappers, lazyCases, providerCases, lazySeen, providerSeen);
            }

            EmitWrapperMethod(
                sb,
                "public global::Stiletto.Internal.Binding GetLazyInjectBinding(string key, object? requiredBy, string lazyKey)",
                switchVar: "lazyKey",
                cases: lazyCases,
                buildCase: w => "new global::Stiletto.Internal.Loaders.Codegen.LazyBinding<" + w.ElementGlobalTypeName + ">(key, requiredBy, lazyKey)");

            EmitWrapperMethod(
                sb,
                "public global::Stiletto.Internal.Binding GetIProviderInjectBinding(string key, object? requiredBy, bool mustBeInjectable, string providerKey)",
                switchVar: "providerKey",
                cases: providerCases,
                buildCase: w => "new global::Stiletto.Internal.Loaders.Codegen.ProviderBinding<" + w.ElementGlobalTypeName + ">(key, requiredBy, mustBeInjectable, providerKey)");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // The public registration entry point. Its [ModuleInitializer] covers the
            // direct-touch path (and assemblies consumed by a non-generated app); the
            // aggregate emitted at Container.Create anchors calls EnsureRegistered()
            // eagerly. It is deliberately stateless: LoaderRegistry.Register dedups by
            // loader type under its lock, so redundant calls (from several threads and
            // paths) coalesce to a single registration with no double-checked flag and
            // thus no memory-model hazard. The type name is unique per assembly so
            // consumers can reference it without a CS0433 collision.
            sb.AppendLine("namespace Stiletto.Generated.Registrations");
            sb.AppendLine("{");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Stiletto.Generator\", null)]");
            sb.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.Append("    public static class ").AppendLine(registrarName);
            sb.AppendLine("    {");
            sb.AppendLine("        public static void EnsureRegistered()");
            sb.AppendLine("            => global::Stiletto.LoaderRegistry.Register(new global::Stiletto.Generated.CompiledLoader());");
            sb.AppendLine();
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("        internal static void Init() => EnsureRegistered();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("Stiletto.Generated.CompiledLoader.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void CollectWrappers(
            EquatableArray<WrapperModel> wrappers,
            List<WrapperModel> lazyCases,
            List<WrapperModel> providerCases,
            HashSet<string> lazySeen,
            HashSet<string> providerSeen)
        {
            foreach (var w in wrappers)
            {
                if (w.IsProvider)
                {
                    if (providerSeen.Add(w.DelegateKey)) providerCases.Add(w);
                }
                else
                {
                    if (lazySeen.Add(w.DelegateKey)) lazyCases.Add(w);
                }
            }
        }

        private static void EmitWrapperMethod(
            StringBuilder sb,
            string signature,
            string switchVar,
            List<WrapperModel> cases,
            System.Func<WrapperModel, string> buildCase)
        {
            if (cases.Count == 0)
            {
                // No such wrappers here — defer to the reflection fallback.
                sb.Append("        ").Append(signature).AppendLine(" => null!;");
                return;
            }

            sb.Append("        ").AppendLine(signature);
            sb.AppendLine("        {");
            sb.Append("            switch (").Append(switchVar).AppendLine(")");
            sb.AppendLine("            {");
            foreach (var w in cases)
            {
                sb.Append("                case ").Append(Literal(w.DelegateKey)).Append(": return ")
                  .Append(buildCase(w)).AppendLine(";");
            }
            sb.AppendLine("                default: return null!;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        private static string GlobalName(string? ns, string typeName)
            => ns is null ? "global::" + typeName : "global::" + ns + "." + typeName;

        /// <summary>
        /// Turns an assembly name into a valid, unique-per-assembly C# identifier for
        /// the public registrar type (e.g. "Foo.Bar-Baz" -> "Foo_Bar_Baz").
        /// </summary>
        private static string SanitizeIdentifier(string assemblyName)
        {
            var sb = new StringBuilder(assemblyName.Length);
            foreach (var c in assemblyName)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            if (sb.Length == 0 || char.IsDigit(sb[0]))
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }

        private static string Literal(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
