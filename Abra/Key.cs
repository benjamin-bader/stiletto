using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra
{
    public class Key
    {
        private const string MemberKeyPrefix = "members/";

        public static string Get<T>()
        {
            return ForType(typeof (T));
        }

        public static string Get(Type t)
        {
            return ForType(t);
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

            return ForType(t, sb);
        }

        public static string GetMemberKey<T>()
        {
            var sb = new StringBuilder(MemberKeyPrefix);
            return ForType(typeof (T), sb);
        }

        public static string GetMemberKey(Type t)
        {
            var sb = new StringBuilder(MemberKeyPrefix);
            return ForType(t, sb);
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

            return key.IndexOf('[') > 0 || key.IndexOf('<') > 0
                ? null
                : key.Substring(start);
        }

        private static string ForType(Type t, StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }
            
            if (t.IsArray)
            {
                sb.Append(ForType(t.GetElementType(), sb));
                sb.Append('[');
                for (int i = 1, rank = t.GetArrayRank(); i < rank; ++i)
                {
                    sb.Append(',');
                }
                sb.Append(']');
            }
            else
            {
                sb.Append(t.FullName);
            }

            return sb.ToString();
        }
    }
}
