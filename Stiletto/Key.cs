/*
 * Copyright © 2013 Ben Bader
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stiletto
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

        /// <summary>
        /// Gets a key representation for the given type <paramref name="t"/>
        /// identified with the given <paramref name="name"/>.
        /// </summary>
        /// <param name="t">
        /// The type whose key is to be returned.
        /// </param>
        /// <param name="name">
        /// The name of the dependency, or <see langword="null"/>.
        /// </param>
        /// <returns>
        /// Returns a type key.
        /// </returns>
        public static string Get(Type t, string name)
        {
            if (string.IsNullOrEmpty(name) && !t.IsGenericType)
            {
                return t.FullName;
            }

            var sb = new StringBuilder();

            if (name != null)
            {
                sb.Append("@").Append(name).Append("/");
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

        /// <summary>
        /// Returns a value indicating whether the given <paramref name="key"/>
        /// is for an object requiring property injection.
        /// </summary>
        public static bool IsPropertyInjection(string key)
        {
            return key.StartsWith(MemberKeyPrefix, Comparison);
        }

        /// <summary>
        /// Returns a value indicating whether the given <paramref name="key"/>
        /// is for a named dependency.
        /// </summary>
        public static bool IsNamed(string key)
        {
            return key.IndexOf('@') >= 0;
        }

        public static string GetTypeName(string key)
        {
            var start = StartOfType(key);

            return key.IndexOf('[') >= 0
                       ? null
                       : start < 0 ? key : key.Substring(start);
        }

        /// <summary>
        /// Returns the key of the underlying binding of a <see cref="IProvider&lt;T&gt;"/>
        /// binding, or <see langword="null"/> if the key is not a lazy binding.
        /// </summary>
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
        public static string GetLazyKey(string key)
        {
            var start = StartOfType(key);
            if (!SubstringStartsWith(key, start, LazyPrefix))
            {
                return null;
            }
            return ExtractKey(key, start, key.Substring(0, start), LazyPrefix);
        }

        private static void ForType(Type t, StringBuilder sb)
        {
            if (t.ContainsGenericParameters)
            {
                throw new ArgumentException("Open generic types are not supported: " + t.AssemblyQualifiedName);
            }

            if (t.IsByRef) {
                throw new ArgumentException("Cannot inject ref or out constructor parameters.");
            }

            if (t.IsGenericType)
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

        private static bool SubstringStartsWith(string str, int offset, string substring)
        {
            return str.IndexOf(substring, offset, Comparison) >= 0;
        }
    }
}
