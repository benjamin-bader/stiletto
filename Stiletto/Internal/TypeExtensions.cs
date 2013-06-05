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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Stiletto.Internal
{
    internal static class TypeExtensions
    {
        internal static bool HasAttribute<TAttribute>(this MemberInfo mi)
            where TAttribute : Attribute
        {
            Conditions.CheckNotNull(mi, "mi");
            return mi.GetCustomAttributes(typeof (TAttribute), false).Length > 0;
        }

        internal static TAttribute GetSingleAttribute<TAttribute>(this MemberInfo mi)
            where TAttribute : Attribute
        {
            Conditions.CheckNotNull(mi, "mi");
            var attrs = mi.GetCustomAttributes(typeof (TAttribute), false);

            if (attrs.Length > 1)
            {
                throw new ArgumentException("Expected at most one attribute of type " + typeof(TAttribute).FullName);
            }

            return attrs.Length == 1 ? (TAttribute) attrs[0] : null;
        }

        internal static TAttribute GetSingleAttribute<TAttribute>(this ParameterInfo pi)
            where TAttribute : Attribute
        {
            Conditions.CheckNotNull(pi, "pi");
            var attrs = pi.GetCustomAttributes(typeof(TAttribute), false);

            if (attrs.Length > 1)
            {
                throw new ArgumentException("Expected at most one attribute of type " + typeof(TAttribute).FullName);
            }

            return attrs.Length == 1 ? (TAttribute)attrs[0] : null;
        }

        internal static bool IsFrameworkType(this Type t)
        {
            Conditions.CheckNotNull(t);

            var assemblyName = t.Assembly.FullName;
            return assemblyName.StartsWith("System", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft", StringComparison.Ordinal)
                || assemblyName.StartsWith("Mono", StringComparison.Ordinal)
                || assemblyName.StartsWith("mscorlib", StringComparison.Ordinal);
        }

        internal static string GetQualifierName(this MemberInfo mi)
        {
            var attr = mi.GetSingleAttribute<NamedAttribute>();
            return attr == null ? null : attr.Name;
        }

        internal static string GetQualifierName(this ParameterInfo pi)
        {
            var attr = pi.GetSingleAttribute<NamedAttribute>();
            return attr == null ? null : attr.Name;
        }
    }
}
