using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stiletto.Generator
{
    /// <summary>
    /// Produces the string keys and names that the Stiletto runtime's
    /// <c>Stiletto.Key</c> / <c>CompilerKeys</c> classes expect, but computed from
    /// Roslyn symbols instead of <c>System.Type</c> / Cecil. These strings are a
    /// wire contract with the runtime <c>Resolver</c>, so they must match byte-for-byte.
    ///
    /// Constructed generics are supported and produce the same backtick-arity form
    /// the runtime does, e.g. <c>System.Collections.Generic.IList`1&lt;System.String&gt;</c>.
    /// <c>Lazy&lt;T&gt;</c> / <c>IProvider&lt;T&gt;</c> need no special handling here —
    /// they are ordinary generics that the resolver recognizes by key prefix.
    /// </summary>
    internal static class RoslynKeys
    {
        public const string MembersPrefix = "members/";
        public const string SetPrefix = "System.Collections.Generic.ISet`1<";
        public const string NamedAttributeMetadataName = "Stiletto.NamedAttribute";
        public const string SingletonAttributeMetadataName = "Stiletto.SingletonAttribute";

        /// <summary>
        /// The CLR reflection full name for a plain (non-generic) named type, e.g.
        /// <c>Sample.Widget</c> — matching <c>Type.FullName</c>. Used for the injected
        /// type itself and base types, both of which are guaranteed non-generic.
        /// </summary>
        public static string ReflectionName(INamedTypeSymbol type)
            => BuildRawName(type);

        /// <summary>
        /// Attempts to build the reflection name for an arbitrary dependency type,
        /// recursing into generic type arguments. Returns false for shapes v1 can't
        /// key faithfully (arrays, pointers, open generics, types nested inside a
        /// generic type), so the caller can fall back to the reflection loader.
        /// </summary>
        public static bool TryReflectionName(ITypeSymbol symbol, out string name)
        {
            name = string.Empty;

            if (symbol is not INamedTypeSymbol type || type.TypeKind == TypeKind.Error || type.IsUnboundGenericType)
            {
                return false;
            }

            // Nested inside a generic type: FullName arity interleaving is an edge
            // case we defer rather than risk a mismatched key.
            for (var container = type.ContainingType; container is not null; container = container.ContainingType)
            {
                if (container.Arity != 0)
                {
                    return false;
                }
            }

            var raw = BuildRawName(type);

            if (type.Arity == 0)
            {
                name = raw;
                return true;
            }

            if (type.TypeArguments.Length != type.Arity)
            {
                return false;
            }

            var args = new List<string>(type.TypeArguments.Length);
            foreach (var arg in type.TypeArguments)
            {
                if (arg.TypeKind == TypeKind.TypeParameter || !TryReflectionName(arg, out var argName))
                {
                    return false;
                }

                args.Add(argName);
            }

            name = raw + "<" + string.Join(",", args) + ">";
            return true;
        }

        /// <summary>
        /// The provider key for a dependency, honoring an optional qualifier name.
        /// Returns false when the type can't be keyed (see <see cref="TryReflectionName"/>).
        /// </summary>
        public static bool TryKeyForType(ITypeSymbol type, string? name, out string key)
        {
            key = string.Empty;
            if (!TryReflectionName(type, out var reflectionName))
            {
                return false;
            }

            key = string.IsNullOrEmpty(name)
                ? reflectionName
                : "@" + name + "/" + reflectionName;
            return true;
        }

        /// <summary>The member (property-injection) key for a type, e.g. <c>members/Sample.Widget</c>.</summary>
        public static string MembersKey(INamedTypeSymbol type)
            => MembersPrefix + BuildRawName(type);

        /// <summary>
        /// Wraps an element key into its set key, mirroring the runtime's
        /// <c>Key.GetSetKey</c>: a qualifier prefix is preserved and the type is
        /// wrapped in <c>ISet`1&lt;...&gt;</c>, e.g. <c>@main/System.String</c> becomes
        /// <c>@main/System.Collections.Generic.ISet`1&lt;System.String&gt;</c>.
        /// </summary>
        public static string GetSetKey(string elementKey)
        {
            var start = elementKey.LastIndexOf('/') + 1; // 0 when unqualified
            return elementKey.Substring(0, start) + SetPrefix + elementKey.Substring(start) + ">";
        }

        /// <summary>Reads the argument of a <c>[Named("...")]</c> attribute on a symbol, or null.</summary>
        public static string? NamedQualifier(ISymbol symbol)
        {
            var attr = symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == NamedAttributeMetadataName);

            if (attr is { ConstructorArguments.Length: > 0 } &&
                attr.ConstructorArguments[0].Value is string name)
            {
                return name;
            }

            return null;
        }

        public static bool HasSingletonAttribute(ISymbol symbol)
            => symbol.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == SingletonAttributeMetadataName);

        /// <summary>
        /// The namespace-qualified, <c>+</c>-nested, backtick-arity name (no type
        /// arguments), e.g. <c>System.Collections.Generic.IList`1</c> — matching the
        /// runtime's <c>GetRawGenericName</c> (which slices <c>Type.FullName</c> at
        /// the first <c>[</c>).
        /// </summary>
        private static string BuildRawName(INamedTypeSymbol type)
        {
            var names = new Stack<string>();
            for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
            {
                names.Push(current.MetadataName); // includes the `arity suffix for generics
            }

            var ns = type.ContainingNamespace;
            var prefix = ns is { IsGlobalNamespace: false }
                ? ns.ToDisplayString() + "."
                : string.Empty;

            return prefix + string.Join("+", names);
        }
    }
}
