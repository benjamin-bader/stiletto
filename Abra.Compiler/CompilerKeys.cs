using System;
using System.IO;
using System.Text;

using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler
{
    public static class CompilerKeys
    {
        private static readonly string LazyPrefix = typeof (Lazy<>).FullName + "<";
        private static readonly string ProviderPrefix = typeof (IProvider<>).FullName + "<";

        public static string ForTypeDef(IType typedef, string name = null)
        {
            if (string.IsNullOrEmpty(name) && !typedef.IsGenericType()) {
                return typedef.ReflectionName;
            }

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(name)) {
                sb.Append("@").Append(name).Append("/");
            }

            ForType(typedef, sb);

            return sb.ToString();
        }

        public static string GetMemberKey(IType typedef)
        {
            var sb = new StringBuilder("members/");
            ForType(typedef, sb);
            return sb.ToString();
        }

        private static void ForType(IType typedef, StringBuilder sb = null)
        {
            if (sb == null) {
                sb = new StringBuilder();
            }

            if (typedef.IsOpen()) {
                throw new ArgumentException("Open generic types are not supported.");
            }

            if (typedef.TypeParameterCount > 0) {
                sb.Append(GetRawGenericName(typedef));
                var t = typedef as ParameterizedType;
                if (t == null) {
                    throw new InvalidDataException("WTF, we have type parameters but are not a parameterized type?");
                }
                sb.Append("<");
                for (var i = 0; i < typedef.TypeParameterCount; ++i) {
                    if (i > 0) {
                        sb.Append(",");
                    }
                    ForType(t.TypeArguments[i], sb);
                }
                sb.Append(">");

            } else {
                // ReflectionName already does the right thing for arrays,
                // so we don't special-case them here.
                sb.Append(typedef.ReflectionName);
            }
        }

        public static string CompilerFriendlyName(IType type, StringBuilder sb = null)
        {
            if (type.TypeParameterCount == 0) {
                return type.FullName;
            }

            sb = sb ?? new StringBuilder(type.FullName).Append("<");

            var parameterizedType = (ParameterizedType) type;
            for (var i = 0; i < type.TypeParameterCount; ++i) {
                if (i > 0) {
                    sb.Append(",");
                }
                sb.Append(CompilerFriendlyName(parameterizedType.TypeArguments[i], sb));
            }
            sb.Append(">");

            return sb.ToString();
        }

        private static string GetRawGenericName(IType typedef)
        {
            var rn = typedef.ReflectionName;
            var index = rn.IndexOf('[');
            return index < 0 ? rn : rn.Substring(0, index);
        }

        public static bool IsNamed(string key)
        {
            return key.IndexOf('@') >= 0;
        }

        public static string GetTypeName(string key)
        {
            var start = StartOfType(key);

            return key.IndexOf('[') >= 0 || key.IndexOf('<') >= 0
                       ? null
                       : start < 0 ? key : key.Substring(start);
        }

        public static string GetProviderKey(string key)
        {
            var start = StartOfType(key);
            if (!SubstringStartsWith(key, start, ProviderPrefix))
            {
                return null;
            }
            return ExtractKey(key, start, key.Substring(0, start), ProviderPrefix);
        }

        /// <summary>
        /// Returns the key of the underlying binding of a <see cref="Lazy&lt;T&gt;"/>
        /// binding, or <see langword="null"/> if the key is not a lazy binding.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetLazyKey(string key)
        {
            var start = StartOfType(key);
            if (!SubstringStartsWith(key, start, LazyPrefix))
            {
                return null;
            }
            return ExtractKey(key, start, key.Substring(0, start), LazyPrefix);
        }

        private static int StartOfType(string key)
        {
            var index = key.LastIndexOf('/');
            return index >= 0 ? index + 1 : 0;
        }


        private static string ExtractKey(string key, int start, string delegatePrefix, string prefix)
        {
            var startIndex = start + prefix.Length;
            return delegatePrefix + key.Substring(startIndex, key.Length - startIndex - 1);
        }

        private static bool SubstringStartsWith(string str, int offset, string substring)
        {
            return str.IndexOf(substring, offset, StringComparison.Ordinal) >= 0;
        }

        public static bool IsArray(this IType typedef)
        {
            return typedef.IsKnownType(KnownTypeCode.Array);
        }

        public static bool IsGenericType(this IType typedef)
        {
            return typedef.TypeParameterCount > 0;
        }
    }
}
