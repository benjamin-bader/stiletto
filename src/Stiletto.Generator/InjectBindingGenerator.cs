using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Stiletto.Generator
{
    /// <summary>
    /// Emits, in C#, the <c>{Type}_CompiledBinding : Stiletto.Internal.Binding</c>
    /// classes that the Fody weaver used to emit in IL. <see cref="StilettoGenerator"/>
    /// wires them into a per-assembly <c>CompiledLoader</c> that self-registers via a
    /// <c>[ModuleInitializer]</c>; the reflection-by-name <c>CodegenLoader</c> remains
    /// only as a fallback.
    ///
    /// v1 scope — the fully-correct subset only. A type is emitted iff it is a
    /// non-generic, non-nested, non-static, non-abstract class whose base is
    /// <see cref="object"/>, is constructable (a public <c>[Inject]</c> constructor
    /// or an accessible parameterless one), and every injected constructor
    /// parameter and <c>[Inject]</c> property is a non-generic named type with, for
    /// properties, an accessible non-init setter. Anything else is left to the
    /// reflection loader, so anything emitted is guaranteed correct.
    /// </summary>
    /// <summary>
    /// Model-building and C# emission for inject bindings. Driven by
    /// <see cref="StilettoGenerator"/> — this is not itself a generator, so the
    /// aggregated loader can never reference a binding this didn't produce.
    /// </summary>
    internal static class InjectBindingEmitter
    {
        internal const string InjectAttributeMetadataName = "Stiletto.InjectAttribute";
        private const string CompiledBindingSuffix = "_CompiledBinding";
        private const string BindingType = "global::Stiletto.Internal.Binding";
        private const string ResolverType = "global::Stiletto.Internal.Resolver";
        private const string SetType = "global::System.Collections.Generic.ISet<" + BindingType + ">";

        internal static InjectBindingModel? BuildModel(INamedTypeSymbol? type)
        {
            if (type is null || !IsSupportedType(type))
            {
                return null;
            }

            // At most one [Inject] constructor; more than one is a user error left
            // to the validator / reflection path.
            var injectCtors = type.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && HasInjectAttribute(c))
                .ToList();
            if (injectCtors.Count > 1)
            {
                return null;
            }

            var ctor = injectCtors.Count == 1
                ? injectCtors[0]
                : type.InstanceConstructors.FirstOrDefault(c =>
                    c.Parameters.Length == 0 &&
                    c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

            // Not constructable -> reflection.
            if (ctor is null)
            {
                return null;
            }

            if (!TryBuildParams(ctor, out var parameters))
            {
                return null;
            }

            if (!TryBuildProperties(type, out var properties))
            {
                return null;
            }

            if (!TryComputeBaseKey(type, out var baseMemberKey))
            {
                return null;
            }

            // Nothing injectable to do (e.g. a plain default ctor with no members).
            var hasInjectCtor = injectCtors.Count == 1;
            if (!hasInjectCtor && properties.Length == 0 && baseMemberKey is null)
            {
                return null;
            }

            var reflectionName = RoslynKeys.ReflectionName(type);
            var ns = type.ContainingNamespace is { IsGlobalNamespace: false } n
                ? n.ToDisplayString()
                : null;

            return new InjectBindingModel(
                Namespace: ns,
                BindingTypeName: type.Name + CompiledBindingSuffix,
                HintName: reflectionName + CompiledBindingSuffix + ".g.cs",
                GlobalTypeName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Key: reflectionName,
                MembersKey: RoslynKeys.MembersKey(type),
                IsSingleton: RoslynKeys.HasSingletonAttribute(type),
                RequiredByCtor: reflectionName + "::.ctor",
                Params: parameters,
                Properties: properties,
                BaseMemberKey: baseMemberKey);
        }

        private static bool TryBuildParams(IMethodSymbol ctor, out ImmutableArray<ParamModel> parameters)
        {
            var builder = ImmutableArray.CreateBuilder<ParamModel>(ctor.Parameters.Length);
            foreach (var p in ctor.Parameters)
            {
                if (!RoslynKeys.TryKeyForType(p.Type, RoslynKeys.NamedQualifier(p), out var key))
                {
                    parameters = default;
                    return false;
                }

                builder.Add(new ParamModel(
                    Key: key,
                    GlobalTypeName: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }

            parameters = builder.MoveToImmutable();
            return true;
        }

        private static bool TryBuildProperties(INamedTypeSymbol type, out ImmutableArray<PropertyModel> properties)
        {
            var builder = ImmutableArray.CreateBuilder<PropertyModel>();
            var reflectionName = RoslynKeys.ReflectionName(type);

            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol prop || !HasInjectAttribute(prop))
                {
                    continue;
                }

                // Needs an accessible, non-init instance setter and a keyable type.
                if (prop.IsStatic
                    || prop.SetMethod is not { IsInitOnly: false } setter
                    || setter.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
                    || !RoslynKeys.TryKeyForType(prop.Type, RoslynKeys.NamedQualifier(prop), out var key))
                {
                    properties = default;
                    return false;
                }

                builder.Add(new PropertyModel(
                    Key: key,
                    GlobalTypeName: prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    PropertyName: prop.Name,
                    RequiredBy: reflectionName + "." + prop.Name));
            }

            properties = builder.ToImmutable();
            return true;
        }

        private static bool IsSupportedType(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class || type.IsStatic || type.IsAbstract)
            {
                return false;
            }

            return IsNonGeneric(type) && type.ContainingType is null;
        }

        /// <summary>
        /// Determines the base type's member key, mirroring <c>ReflectionInjectBinding</c>:
        /// a base binding is chained iff the immediate base is non-framework and has
        /// its own inject members. Returns false (skip the whole type to reflection)
        /// only when the base is injectable but generic, since v1 can't form its key.
        /// </summary>
        private static bool TryComputeBaseKey(INamedTypeSymbol type, out string? baseMemberKey)
        {
            baseMemberKey = null;

            var baseType = type.BaseType;
            if (baseType is null
                || baseType.SpecialType == SpecialType.System_Object
                || IsFrameworkType(baseType)
                || !HasInjectMembers(baseType))
            {
                return true;
            }

            if (!IsNonGeneric(baseType))
            {
                return false;
            }

            baseMemberKey = RoslynKeys.MembersKey(baseType);
            return true;
        }

        private static bool IsFrameworkType(INamedTypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
            {
                return true;
            }

            var assembly = type.ContainingAssembly?.Name ?? string.Empty;
            return assembly.StartsWith("System", StringComparison.Ordinal)
                || assembly.StartsWith("mscorlib", StringComparison.Ordinal)
                || assembly.StartsWith("Microsoft", StringComparison.Ordinal)
                || assembly.StartsWith("Mono", StringComparison.Ordinal)
                || assembly == "netstandard";
        }

        private static bool HasInjectMembers(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor && HasInjectAttribute(ctor))
                {
                    return true;
                }

                if (member is IPropertySymbol prop && HasInjectAttribute(prop))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNonGeneric(INamedTypeSymbol type)
            => type.Arity == 0 && !type.IsGenericType && !type.IsUnboundGenericType;

        private static bool HasInjectAttribute(ISymbol symbol)
            => symbol.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == InjectAttributeMetadataName);

        internal static SourceText Emit(InjectBindingModel model)
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

            var hasParams = model.Params.Count > 0;
            var hasProperties = model.Properties.Count > 0;
            var hasBase = model.BaseMemberKey is not null;
            var injectsProperties = hasProperties || hasBase;

            sb.Append(indent).AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Stiletto.Generator\", null)]");
            sb.Append(indent).AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
            sb.Append(indent).Append("internal sealed class ").Append(model.BindingTypeName)
              .Append(" : ").AppendLine(BindingType);
            sb.Append(indent).AppendLine("{");

            var body = indent + "    ";
            var stmt = body + "    ";

            if (hasParams)
            {
                sb.Append(body).Append("private ").Append(BindingType).AppendLine("[] ctorParamBindings = null!;");
            }
            foreach (var prop in model.Properties)
            {
                sb.Append(body).Append("private ").Append(BindingType).Append(' ').Append(FieldName(prop)).AppendLine(" = null!;");
            }
            if (hasBase)
            {
                sb.Append(body).Append("private ").Append(BindingType).AppendLine(" baseTypeBinding = null!;");
            }
            if (hasParams || hasProperties || hasBase)
            {
                sb.AppendLine();
            }

            // Constructor: base(key, membersKey, isSingleton, typeof(T))
            sb.Append(body).Append("public ").Append(model.BindingTypeName).AppendLine("()");
            sb.Append(stmt).Append(": base(")
              .Append(Literal(model.Key)).Append(", ")
              .Append(Literal(model.MembersKey)).Append(", ")
              .Append(model.IsSingleton ? "true" : "false").Append(", ")
              .Append("typeof(").Append(model.GlobalTypeName).AppendLine("))");
            sb.Append(body).AppendLine("{");
            sb.Append(body).AppendLine("}");
            sb.AppendLine();

            // Resolve
            sb.Append(body).Append("public override void Resolve(").Append(ResolverType).AppendLine(" resolver)");
            sb.Append(body).AppendLine("{");
            if (hasParams)
            {
                sb.Append(stmt).Append("var bindings = new ").Append(BindingType).Append('[').Append(model.Params.Count).AppendLine("];");
                for (var i = 0; i < model.Params.Count; ++i)
                {
                    sb.Append(stmt).Append("bindings[").Append(i).Append("] = resolver.RequestBinding(")
                      .Append(Literal(model.Params[i].Key)).Append(", ")
                      .Append(Literal(model.RequiredByCtor)).AppendLine(", true, true);");
                }
                sb.Append(stmt).AppendLine("this.ctorParamBindings = bindings;");
            }
            foreach (var prop in model.Properties)
            {
                sb.Append(stmt).Append("this.").Append(FieldName(prop)).Append(" = resolver.RequestBinding(")
                  .Append(Literal(prop.Key)).Append(", ")
                  .Append(Literal(prop.RequiredBy)).AppendLine(", true, false);");
            }
            if (hasBase)
            {
                sb.Append(stmt).Append("this.baseTypeBinding = resolver.RequestBinding(")
                  .Append(Literal(model.BaseMemberKey!)).Append(", ")
                  .Append(Literal(model.MembersKey)).AppendLine(", false, false);");
            }
            sb.Append(body).AppendLine("}");
            sb.AppendLine();

            // GetDependencies
            sb.Append(body).AppendLine("public override void GetDependencies(");
            sb.Append(stmt).Append(SetType).AppendLine(" injectDependencies,");
            sb.Append(stmt).Append(SetType).AppendLine(" propertyDependencies)");
            sb.Append(body).AppendLine("{");
            if (hasParams)
            {
                sb.Append(stmt).AppendLine("injectDependencies.UnionWith(this.ctorParamBindings);");
            }
            foreach (var prop in model.Properties)
            {
                sb.Append(stmt).Append("propertyDependencies.Add(this.").Append(FieldName(prop)).AppendLine(");");
            }
            if (hasBase)
            {
                sb.Append(stmt).AppendLine("propertyDependencies.Add(this.baseTypeBinding);");
            }
            sb.Append(body).AppendLine("}");
            sb.AppendLine();

            // InjectProperties (when there are own properties and/or an injectable base)
            if (injectsProperties)
            {
                sb.Append(body).AppendLine("public override void InjectProperties(object target)");
                sb.Append(body).AppendLine("{");
                sb.Append(stmt).Append("var inject = (").Append(model.GlobalTypeName).AppendLine(")target;");
                foreach (var prop in model.Properties)
                {
                    sb.Append(stmt).Append("inject.").Append(prop.PropertyName).Append(" = (")
                      .Append(prop.GlobalTypeName).Append(")this.").Append(FieldName(prop)).AppendLine(".Get();");
                }
                if (hasBase)
                {
                    // Inject the base type's members into the same instance, after our own.
                    sb.Append(stmt).AppendLine("this.baseTypeBinding.InjectProperties(inject);");
                }
                sb.Append(body).AppendLine("}");
                sb.AppendLine();
            }

            // Get
            sb.Append(body).AppendLine("public override object Get()");
            sb.Append(body).AppendLine("{");
            if (injectsProperties)
            {
                EmitConstruction(sb, model, stmt, "var result = ");
                sb.Append(stmt).AppendLine("this.InjectProperties(result);");
                sb.Append(stmt).AppendLine("return result;");
            }
            else
            {
                EmitConstruction(sb, model, stmt, "return ");
            }
            sb.Append(body).AppendLine("}");

            sb.Append(indent).AppendLine("}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        private static void EmitConstruction(StringBuilder sb, InjectBindingModel model, string stmt, string lead)
        {
            if (model.Params.Count == 0)
            {
                sb.Append(stmt).Append(lead).Append("new ").Append(model.GlobalTypeName).AppendLine("();");
                return;
            }

            sb.Append(stmt).Append(lead).Append("new ").Append(model.GlobalTypeName).AppendLine("(");
            for (var i = 0; i < model.Params.Count; ++i)
            {
                var comma = i == model.Params.Count - 1 ? string.Empty : ",";
                sb.Append(stmt).Append("    (").Append(model.Params[i].GlobalTypeName)
                  .Append(")this.ctorParamBindings[").Append(i).Append("].Get()").Append(comma).AppendLine();
            }
            sb.Append(stmt).AppendLine(");");
        }

        private static string FieldName(PropertyModel prop) => "prop_" + prop.PropertyName;

        private static string Literal(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    internal sealed record ParamModel(string Key, string GlobalTypeName);

    internal sealed record PropertyModel(string Key, string GlobalTypeName, string PropertyName, string RequiredBy);

    internal sealed record InjectBindingModel(
        string? Namespace,
        string BindingTypeName,
        string HintName,
        string GlobalTypeName,
        string Key,
        string MembersKey,
        bool IsSingleton,
        string RequiredByCtor,
        EquatableArray<ParamModel> Params,
        EquatableArray<PropertyModel> Properties,
        string? BaseMemberKey);
}
