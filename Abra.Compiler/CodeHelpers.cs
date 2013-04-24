using System;
using System.Collections.Generic;
using System.Text;

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace Abra.Compiler
{
    public static class CodeHelpers
    {
        /// <summary>
        /// Gets the C# access modifier for the compiled wrapper of a given
        /// type.  Note that this does *not* return the access modifier of the
        /// type itself - it returns that of its wrapper.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string AccessibilityName(ITypeDefinition type)
        {
            switch (type.Accessibility) {
                case Accessibility.Public: return "public";
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal: return "internal";
                default:
                    throw new ArgumentException("Only public or internal classes may ");
            }
        }

        /// <summary>
        /// Gets a value indicating whether the given <paramref name="entity"/>
        /// is visible to other non-nesting classes in its parent assembly.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>
        /// Returns <see langword="true"/> if the entity is <see langword="public"/>,
        /// <see langword="internal"/>, or <see langword="protected internal"/>;
        /// returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsPublicOrInternal(this IEntity entity)
        {
            return entity.Accessibility == Accessibility.Public
                || entity.Accessibility == Accessibility.Internal
                || entity.Accessibility == Accessibility.ProtectedOrInternal;
        }

        /// <summary>
        /// Constructs the name used in C# code to reference the given
        /// <paramref name="type"/>.
        /// </summary>
        /// <remarks>
        /// The code-literal of a type is the text that would appear in a C#
        /// program.  For example, an <see cref="ITypeDefinition"/> for the string
        /// type would be "String".  For a list of ints, it would be "List&lt;int&gt;".
        /// </remarks>
        /// <param name="type">
        /// The type whose C# representation is to be constructed.
        /// </param>
        /// <returns>
        /// The C# text identifying the given <paramref name="type"/>.
        /// </returns>
        public static string ToCodeLiteral(ITypeDefinition type)
        {
            if (type.TypeParameterCount == 0 && type.DeclaringType is UnknownType) {
                return type.Name;
            }

            var nestingTypes = new List<ITypeDefinition> {type};

            var t = type.DeclaringType;
            while (t != null && !(t is UnknownType)) {
                nestingTypes.Add(t.GetDefinition());
                t = t.DeclaringType;
            }

            var sb = new StringBuilder();
            nestingTypes.Reverse();
            foreach (var def in nestingTypes) {
                ToCodeLiteral(def, sb);
                sb.Append(".");
            }
            return sb.ToString(0, sb.Length - 1);
        }

        private static void ToCodeLiteral(ITypeDefinition type, StringBuilder sb)
        {
            if (sb == null) {
                sb = new StringBuilder();
            }

            sb.Append(type.Name);

            if (type.TypeParameterCount != 0) {
                sb.Append("<");
                for (var i = 0; i < type.TypeParameterCount; ++i) {
                    if (i > 0) {
                        sb.Append(",");
                    }
                    var param = type.TypeParameters[0];
                    ToCodeLiteral(param, sb);
                }
                sb.Append(">");
            }
        }

        private static void ToCodeLiteral(ITypeParameter param, StringBuilder sb)
        {
            sb.Append(param.FullName);

            if (param.TypeParameterCount != 0) {
                var def = param.GetDefinition();
                sb.Append("<");
                for (var i = 0; i < def.TypeParameterCount; ++i) {
                    if (i > 0) {
                        sb.Append(",");
                    }
                    var p = def.TypeParameters[i];
                    ToCodeLiteral(p, sb);
                }
                sb.Append(">");
            }
        }
    }
}
