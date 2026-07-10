using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Stiletto.Generator
{
    /// <summary>
    /// Emits, in C#, the <c>{Module}_CompiledModule : Stiletto.Internal.RuntimeModule</c>
    /// class the Fody weaver used to emit in IL, with a nested <c>ProviderBinding_N :
    /// Binding</c> per <c>[Provides]</c> method. The runtime <c>CodegenLoader</c>
    /// discovers it by the <c>_CompiledModule</c> name; no <c>$CompiledLoader$</c> needed.
    ///
    /// v1 scope: <c>[Module]</c> classes that are non-generic, non-nested, non-abstract,
    /// derive from <see cref="object"/>, have a public default constructor, and whose
    /// every <c>[Provides]</c> method is public, non-static, non-generic, returns a
    /// keyable non-Lazy/non-IProvider type, and is <c>ProvidesType.Default</c> (Set
    /// providers force the whole module to the reflection loader). Anything else falls
    /// back to reflection, so anything emitted is guaranteed correct.
    ///
    /// Not itself a generator: <see cref="StilettoGenerator"/> drives it.
    /// </summary>
    internal static class ModuleEmitter
    {
        internal const string ModuleAttributeMetadataName = "Stiletto.ModuleAttribute";
        private const string ProvidesAttributeMetadataName = "Stiletto.ProvidesAttribute";
        private const string CompiledModuleSuffix = "_CompiledModule";
        private const string BindingType = "global::Stiletto.Internal.Binding";
        private const string ResolverType = "global::Stiletto.Internal.Resolver";
        private const string SetType = "global::System.Collections.Generic.ISet<" + BindingType + ">";
        private const string DictionaryType = "global::System.Collections.Generic.IDictionary<string, " + BindingType + ">";

        internal static ModuleModel? BuildModel(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class
                || type.IsStatic
                || type.IsAbstract
                || type.Arity != 0
                || type.ContainingType is not null
                || type.BaseType is not { SpecialType: SpecialType.System_Object }
                || !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))
            {
                return null;
            }

            var moduleReflectionName = RoslynKeys.ReflectionName(type);

            if (!TryReadModuleAttribute(type, out var complete, out var isLibrary, out var isOverride, out var injects, out var includes))
            {
                return null;
            }

            var wrappers = ImmutableArray.CreateBuilder<WrapperModel>();
            if (!TryBuildProviders(type, moduleReflectionName, wrappers, out var providers))
            {
                return null;
            }

            var ns = type.ContainingNamespace is { IsGlobalNamespace: false } n
                ? n.ToDisplayString()
                : null;

            return new ModuleModel(
                Namespace: ns,
                CompiledTypeName: type.Name + CompiledModuleSuffix,
                HintName: moduleReflectionName + CompiledModuleSuffix + ".g.cs",
                ReflectionName: moduleReflectionName,
                ModuleGlobalTypeName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsComplete: complete,
                IsLibrary: isLibrary,
                IsOverride: isOverride,
                InjectMemberKeys: injects,
                IncludeGlobalTypeNames: includes,
                Providers: providers,
                Wrappers: wrappers.ToImmutable());
        }

        private static bool TryReadModuleAttribute(
            INamedTypeSymbol type,
            out bool complete,
            out bool isLibrary,
            out bool isOverride,
            out ImmutableArray<string> injects,
            out ImmutableArray<string> includes)
        {
            complete = true; // ModuleAttribute defaults
            isLibrary = false;
            isOverride = false;
            injects = ImmutableArray<string>.Empty;
            includes = ImmutableArray<string>.Empty;

            var attr = type.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == ModuleAttributeMetadataName);
            if (attr is null)
            {
                return false;
            }

            var injectBuilder = ImmutableArray.CreateBuilder<string>();
            var includeBuilder = ImmutableArray.CreateBuilder<string>();

            foreach (var arg in attr.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "IsComplete" when arg.Value.Value is bool c:
                        complete = c;
                        break;
                    case "IsLibrary" when arg.Value.Value is bool l:
                        isLibrary = l;
                        break;
                    case "IsOverride" when arg.Value.Value is bool o:
                        isOverride = o;
                        break;
                    case "Injects":
                        foreach (var element in arg.Value.Values)
                        {
                            if (element.Value is not INamedTypeSymbol injectType
                                || !RoslynKeys.TryReflectionName(injectType, out var reflectionName))
                            {
                                return false;
                            }

                            injectBuilder.Add(RoslynKeys.MembersPrefix + reflectionName);
                        }
                        break;
                    case "IncludedModules":
                        foreach (var element in arg.Value.Values)
                        {
                            if (element.Value is not INamedTypeSymbol includeType)
                            {
                                return false;
                            }

                            includeBuilder.Add(includeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                        break;
                }
            }

            injects = injectBuilder.ToImmutable();
            includes = includeBuilder.ToImmutable();
            return true;
        }

        private static bool TryBuildProviders(INamedTypeSymbol type, string moduleReflectionName, ImmutableArray<WrapperModel>.Builder wrappers, out ImmutableArray<ProviderModel> providers)
        {
            providers = default;
            var builder = ImmutableArray.CreateBuilder<ProviderModel>();

            foreach (var member in type.GetMembers())
            {
                if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
                {
                    continue;
                }

                var provides = method.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.ToDisplayString() == ProvidesAttributeMetadataName);
                if (provides is null)
                {
                    continue;
                }

                // ProvidesType.Default == 0, ProvidesType.Set == 1; anything else is unknown.
                var providesType = provides.ConstructorArguments.Length > 0
                    ? Convert.ToInt32(provides.ConstructorArguments[0].Value)
                    : 0;
                if (providesType is < 0 or > 1
                    || method.IsStatic
                    || method.IsAbstract
                    || method.IsGenericMethod
                    || method.DeclaredAccessibility != Accessibility.Public
                    || method.ReturnsVoid
                    || IsLazyOrProvider(method.ReturnType)
                    || !RoslynKeys.TryKeyForType(method.ReturnType, RoslynKeys.NamedQualifier(method), out var returnKey))
                {
                    return false;
                }

                var paramBuilder = ImmutableArray.CreateBuilder<ProviderParamModel>(method.Parameters.Length);
                foreach (var p in method.Parameters)
                {
                    var qualifier = RoslynKeys.NamedQualifier(p);
                    if (!RoslynKeys.TryKeyForType(p.Type, qualifier, out var paramKey))
                    {
                        return false;
                    }

                    paramBuilder.Add(new ProviderParamModel(
                        Key: paramKey,
                        GlobalTypeName: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

                    InjectBindingEmitter.AddWrapperIfAny(p.Type, qualifier, wrappers);
                }

                builder.Add(new ProviderModel(
                    Key: returnKey,
                    MethodName: method.Name,
                    IsSingleton: RoslynKeys.HasSingletonAttribute(method),
                    IsSet: providesType == 1,
                    ReturnGlobalTypeName: method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    RequiredBy: moduleReflectionName + "." + method.Name,
                    Params: paramBuilder.MoveToImmutable()));
            }

            providers = builder.ToImmutable();
            return true;
        }

        private static bool IsLazyOrProvider(ITypeSymbol type)
            => RoslynKeys.IsLazy(type) || RoslynKeys.IsProvider(type);

        internal static SourceText Emit(ModuleModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");

            var indent = string.Empty;
            var hasNamespace = model.Namespace is not null;
            if (hasNamespace)
            {
                sb.Append("namespace ").AppendLine(model.Namespace);
                sb.AppendLine("{");
                indent = "    ";
            }

            var body = indent + "    ";

            sb.Append(indent).AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Stiletto.Generator\", null)]");
            sb.Append(indent).AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.Append(indent).Append("internal sealed class ").Append(model.CompiledTypeName)
              .AppendLine(" : global::Stiletto.Internal.RuntimeModule");
            sb.Append(indent).AppendLine("{");

            EmitModuleCtor(sb, model, body);
            sb.AppendLine();
            EmitCreateModule(sb, model, body);
            sb.AppendLine();
            EmitGetBindings(sb, model, body);

            for (var i = 0; i < model.Providers.Count; ++i)
            {
                sb.AppendLine();
                EmitProviderBinding(sb, model, model.Providers[i], i, body);
            }

            sb.Append(indent).AppendLine("}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        private static void EmitModuleCtor(StringBuilder sb, ModuleModel model, string body)
        {
            var stmt = body + "    ";
            sb.Append(body).Append("public ").Append(model.CompiledTypeName).AppendLine("()");
            sb.Append(stmt).AppendLine(": base(");
            sb.Append(stmt).Append("    typeof(").Append(model.ModuleGlobalTypeName).AppendLine("),");

            sb.Append(stmt).Append("    new string[] { ")
              .Append(string.Join(", ", model.InjectMemberKeys.Select(Literal))).AppendLine(" },");
            sb.Append(stmt).Append("    new global::System.Type[] { ")
              .Append(string.Join(", ", model.IncludeGlobalTypeNames.Select(t => "typeof(" + t + ")"))).AppendLine(" },");

            sb.Append(stmt).Append("    ").Append(Bool(model.IsComplete)).AppendLine(",");
            sb.Append(stmt).Append("    ").Append(Bool(model.IsLibrary)).AppendLine(",");
            sb.Append(stmt).Append("    ").Append(Bool(model.IsOverride)).AppendLine(")");
            sb.Append(body).AppendLine("{");
            sb.Append(body).AppendLine("}");
        }

        private static void EmitCreateModule(StringBuilder sb, ModuleModel model, string body)
        {
            var stmt = body + "    ";
            sb.Append(body).AppendLine("public override object CreateModule()");
            sb.Append(body).AppendLine("{");
            sb.Append(stmt).Append("return new ").Append(model.ModuleGlobalTypeName).AppendLine("();");
            sb.Append(body).AppendLine("}");
        }

        private static void EmitGetBindings(StringBuilder sb, ModuleModel model, string body)
        {
            var stmt = body + "    ";
            sb.Append(body).Append("public override void GetBindings(").Append(DictionaryType).AppendLine(" bindings)");
            sb.Append(body).AppendLine("{");
            if (model.Providers.Count > 0)
            {
                sb.Append(stmt).Append("var module = (").Append(model.ModuleGlobalTypeName).AppendLine(")this.Module;");
                for (var i = 0; i < model.Providers.Count; ++i)
                {
                    var provider = model.Providers[i];
                    if (provider.IsSet)
                    {
                        // Contribute to (creating if needed) the ISet<T> binding at the set key.
                        sb.Append(stmt).Append("global::Stiletto.Internal.Loaders.Codegen.SetBindings.Add<")
                          .Append(provider.ReturnGlobalTypeName).Append(">(bindings, ")
                          .Append(Literal(RoslynKeys.GetSetKey(provider.Key)))
                          .Append(", new ProviderBinding_").Append(i).AppendLine("(module));");
                    }
                    else
                    {
                        sb.Append(stmt).Append("bindings.Add(").Append(Literal(provider.Key))
                          .Append(", new ProviderBinding_").Append(i).AppendLine("(module));");
                    }
                }
            }
            sb.Append(body).AppendLine("}");
        }

        private static void EmitProviderBinding(StringBuilder sb, ModuleModel model, ProviderModel provider, int index, string body)
        {
            var member = body + "    ";
            var stmt = member + "    ";
            var hasParams = provider.Params.Count > 0;

            sb.Append(body).AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.Append(body).Append("private sealed class ProviderBinding_").Append(index).Append(" : ").AppendLine(BindingType);
            sb.Append(body).AppendLine("{");

            sb.Append(member).Append("private readonly ").Append(model.ModuleGlobalTypeName).AppendLine(" module;");
            for (var i = 0; i < provider.Params.Count; ++i)
            {
                sb.Append(member).Append("private ").Append(BindingType).Append(" arg").Append(i).AppendLine(" = null!;");
            }
            sb.AppendLine();

            // Constructor: base(key, null, isSingleton, "Module.Method")
            sb.Append(member).Append("public ProviderBinding_").Append(index).Append('(').Append(model.ModuleGlobalTypeName).AppendLine(" module)");
            sb.Append(stmt).Append(": base(").Append(Literal(provider.Key)).Append(", null, ")
              .Append(Bool(provider.IsSingleton)).Append(", ").Append(Literal(provider.RequiredBy)).AppendLine(")");
            sb.Append(member).AppendLine("{");
            sb.Append(stmt).AppendLine("this.module = module;");
            if (model.IsLibrary)
            {
                sb.Append(stmt).AppendLine("this.IsLibrary = true;");
            }
            sb.Append(member).AppendLine("}");

            // Resolve / GetDependencies only when there are parameters (base no-ops otherwise).
            if (hasParams)
            {
                sb.AppendLine();
                sb.Append(member).Append("public override void Resolve(").Append(ResolverType).AppendLine(" resolver)");
                sb.Append(member).AppendLine("{");
                for (var i = 0; i < provider.Params.Count; ++i)
                {
                    sb.Append(stmt).Append("this.arg").Append(i).Append(" = resolver.RequestBinding(")
                      .Append(Literal(provider.Params[i].Key)).Append(", typeof(").Append(model.ModuleGlobalTypeName)
                      .Append("), true, ").Append(Bool(model.IsLibrary)).AppendLine(");");
                }
                sb.Append(member).AppendLine("}");

                sb.AppendLine();
                sb.Append(member).AppendLine("public override void GetDependencies(");
                sb.Append(stmt).Append(SetType).AppendLine(" injectDependencies,");
                sb.Append(stmt).Append(SetType).AppendLine(" propertyDependencies)");
                sb.Append(member).AppendLine("{");
                for (var i = 0; i < provider.Params.Count; ++i)
                {
                    sb.Append(stmt).Append("injectDependencies.Add(this.arg").Append(i).AppendLine(");");
                }
                sb.Append(member).AppendLine("}");
            }

            // Get: invoke the provider method (value types box implicitly on return).
            sb.AppendLine();
            sb.Append(member).AppendLine("public override object Get()");
            sb.Append(member).AppendLine("{");
            if (hasParams)
            {
                sb.Append(stmt).Append("return this.module.").Append(provider.MethodName).AppendLine("(");
                for (var i = 0; i < provider.Params.Count; ++i)
                {
                    var comma = i == provider.Params.Count - 1 ? string.Empty : ",";
                    sb.Append(stmt).Append("    (").Append(provider.Params[i].GlobalTypeName)
                      .Append(")this.arg").Append(i).Append(".Get()").Append(comma).AppendLine();
                }
                sb.Append(stmt).AppendLine(");");
            }
            else
            {
                sb.Append(stmt).Append("return this.module.").Append(provider.MethodName).AppendLine("();");
            }
            sb.Append(member).AppendLine("}");

            sb.Append(body).AppendLine("}");
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Literal(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    internal sealed record ProviderParamModel(string Key, string GlobalTypeName);

    internal sealed record ProviderModel(
        string Key,
        string MethodName,
        bool IsSingleton,
        bool IsSet,
        string ReturnGlobalTypeName,
        string RequiredBy,
        EquatableArray<ProviderParamModel> Params);

    internal sealed record ModuleModel(
        string? Namespace,
        string CompiledTypeName,
        string HintName,
        string ReflectionName,
        string ModuleGlobalTypeName,
        bool IsComplete,
        bool IsLibrary,
        bool IsOverride,
        EquatableArray<string> InjectMemberKeys,
        EquatableArray<string> IncludeGlobalTypeNames,
        EquatableArray<ProviderModel> Providers,
        EquatableArray<WrapperModel> Wrappers);
}
