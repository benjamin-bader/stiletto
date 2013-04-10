using System;
using System.Collections.Generic;
using System.Text;

namespace Abra
{
    public class Key
    {
        private const string MemberKeyPrefix = "members/";
        private static readonly string LazyPrefix = GetRawGenericName(typeof (Lazy<object>)) + "<";
        private static readonly string ProviderPrefix = GetRawGenericName(typeof (IProvider<object>)) + "<";

        /// <summary>
        /// An <see cref="IEqualityComparer&lt;String&gt;"/> instance suitable
        /// for comparing keys.
        /// </summary>
        public static readonly IEqualityComparer<string> Comparer = StringComparer.Ordinal; 

        /// <summary>
        /// A <see cref="StringComparison"/> suitable for comparing keys.
        /// </summary>
        public static readonly StringComparison Comparison = StringComparison.Ordinal;

        /// <summary>
        /// Gets a key representation for the given type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>
        /// Returns a type key.
        /// </returns>
        public static string Get<T>()
        {
            return Get(typeof (T), null);
        }

        /// <summary>
        /// Gets a key representation for the given type <paramref name="t"/>
        /// </summary>
        /// <param name="t"></param>
        /// <returns>
        /// Returns a type key.
        /// </returns>
        public static string Get(Type t)
        {
            return Get(t, null);
        }

        public static string Get(Type t, string name)
        {
            if (string.IsNullOrEmpty(name) && !t.IsArray && !t.IsGenericType)
            {
                return t.AssemblyQualifiedName;
            }

            var sb = new StringBuilder();

            if (name != null)
            {
                sb.Append("@").Append(name);
            }

            ForType(t, sb);

            return sb.ToString();
        }

        public static string GetMemberKey<T>()
        {
            var sb = new StringBuilder(MemberKeyPrefix);
            ForType(typeof (T), sb);
            return sb.ToString();
        }

        public static string GetMemberKey(Type t)
        {
            var sb = new StringBuilder(MemberKeyPrefix);
            ForType(t, sb);
            return sb.ToString();
        }

        public static bool IsPropertyInjection(string key)
        {
            return key.StartsWith(MemberKeyPrefix, Comparison);
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
            if (!SubstringStartsWith(key, start, ProviderPrefix)) {
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

        private static void ForType(Type t, StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            if (t.ContainsGenericParameters)
            {
                throw new ArgumentException("Open generic types are not supported: " + t.AssemblyQualifiedName);
            }
            
            if (t.IsArray)
            {
                ForType(t.GetElementType(), sb);
                sb.Append('[');
                for (int i = 1, rank = t.GetArrayRank(); i < rank; ++i)
                {
                    sb.Append(',');
                }
                sb.Append(']');
            }
            else if (t.IsGenericType)
            {
                sb.Append(GetRawGenericName(t));
                var parameters = t.GetGenericArguments();
                sb.Append('<');
                for (var i = 0; i < parameters.Length; ++i)
                {
                    if (i != 0)
                    {
                        sb.Append(',');
                    }
                    ForType(parameters[i], sb);
                }
                sb.Append('>');
            }
            else
            {
                sb.Append(t.AssemblyQualifiedName);
            }
        }

        private static string GetRawGenericName(Type t)
        {
            var name = t.AssemblyQualifiedName;
            var genericParametersStart = name.IndexOf('[');
            return name.Substring(0, genericParametersStart);
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
            return str.IndexOf(substring, offset, Comparison) >= 0;
        }
    }
}
