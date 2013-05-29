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
using System.Text;
using Mono.Cecil;

namespace Stiletto.Fody
{
    public static class CompilerKeys
    {
        private static readonly string LazyPrefix = typeof(Lazy<>).FullName + "<";
        private static readonly string ProviderPrefix = typeof(IProvider<>).FullName + "<";

        public static string ForParam(ParameterDefinition param)
        {
            var name = param.GetNamedAttributeName();
            return ForType(param.ParameterType, name);
        }

        public static string ForProperty(PropertyDefinition property)
        {
            var name = property.GetNamedAttributeName();
            return ForType(property.PropertyType, name);
        }

        public static string ForReturnType(MethodReturnType methodReturnType)
        {
            var name = methodReturnType.GetNamedAttributeName();
            return ForType(methodReturnType.ReturnType, name);
        }

        public static string ForType(TypeReference typedef, string name = null)
        {
            if (string.IsNullOrEmpty(name) && !(typedef is GenericInstanceType))
            {
                return typedef.GetReflectionName();
            }

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(name))
            {
                sb.Append("@").Append(name).Append("/");
            }

            ForType(typedef, sb);

            return sb.ToString();
        }

        public static string GetMemberKey(TypeReference typedef)
        {
            var sb = new StringBuilder("members/");
            ForType(typedef, sb);
            return sb.ToString();
        }

        private static void ForType(TypeReference typedef, StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            if (typedef.HasGenericParameters)
            {
                throw new ArgumentException("Open generic types are not supported.");
            }

            if (typedef is GenericInstanceType) {
                var genericType = (GenericInstanceType) typedef;
                sb.Append(GetRawGenericName(typedef));

                sb.Append("<");
                for (var i = 0; i < genericType.GenericArguments.Count; ++i)
                {
                    if (i > 0)
                    {
                        sb.Append(",");
                    }
                    ForType(genericType.GenericArguments[i], sb);
                }
                sb.Append(">");

            }
            else
            {
                sb.Append(typedef.GetReflectionName());
            }
        }

        private static string GetRawGenericName(TypeReference typedef)
        {
            var rn = typedef.GetReflectionName();
            var index = rn.IndexOf('<');
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

        private static string GetReflectionName(this TypeReference reference)
        {
            return reference.FullName.Replace('/', '+');
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
    }
}
