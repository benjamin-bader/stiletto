using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra
{
    public class Key
    {
        private const string MemberKeyPrefix = "members/";
        private static readonly string LazyPrefix = GetRawGenericName(typeof (Lazy<object>)) + "<";

        public static readonly IEqualityComparer<string> Comparer = StringComparer.Ordinal; 
        public static readonly StringComparison Comparison = StringComparison.Ordinal;

        public static string Get<T>()
        {
            return Get(typeof (T), null);
        }

        public static string Get(Type t)
        {
            return Get(t, null);
        }

        public static string Get(Type t, string name)
        {
            if (string.IsNullOrEmpty(name) && !t.IsArray)
            {
                return t.FullName;
            }

            var sb = new StringBuilder('@')
                .Append(name)
                .Append('/');

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
            return key.StartsWith(MemberKeyPrefix, StringComparison.Ordinal);
        }

        public static bool IsNamed(string key)
        {
            return key.IndexOf('@') >= 0;
        }

        public static string GetTypeName(string key)
        {
            var start = key.LastIndexOf('/');

            return key.IndexOf('[') >= 0 || key.IndexOf('<') >= 0
                       ? null
                       : start < 0 ? key : key.Substring(start);
        }

        private static void ForType(Type t, StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            if (t.ContainsGenericParameters)
            {
                throw new ArgumentException("Open generic types are not supported: " + t.FullName);
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
                sb.Append(t.FullName);
            }
        }

        private static string GetRawGenericName(Type t)
        {
            var name = t.FullName;
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

        public static string GetBuiltInKey(string key)
        {
            return null;
        }

        public static string GetLazyKey(string key)
        {
            var start = StartOfType(key);
            if (!SubstringStartsWith(key, start, LazyPrefix))
            {
                return null;
            }

            return ExtractKey(key, start, key.Substring(0, start), LazyPrefix);
        }

        private static bool SubstringStartsWith(string str, int offset, string substring)
        {
            return str.IndexOf(substring, offset, Comparison) >= 0;
        }
    }
}
