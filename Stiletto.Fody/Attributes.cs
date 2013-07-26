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
using System.Linq;
﻿using Mono.Cecil;

namespace Stiletto.Fody
{
    public static class Attributes
    {
        private const string InjectAttributeName = "Stiletto.InjectAttribute";
        private const string ModuleAttributeName = "Stiletto.ModuleAttribute";
        private const string ProvidesAttributeName = "Stiletto.ProvidesAttribute";
        private const string NamedAttributeName = "Stiletto.NamedAttribute";
        private const string SingletonAttributeName = "Stiletto.SingletonAttribute";
        private const string ProcessedAssemblyAttributeName = "Stiletto.Internal.Loaders.Codegen.ProcessedAssemblyAttribute";

        public static bool IsInjectAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, InjectAttributeName);
        }

        public static bool IsModuleAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, ModuleAttributeName);
        }

        public static bool IsProvidesAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, ProvidesAttributeName);
        }

        public static bool IsNamedAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, NamedAttributeName);
        }

        public static bool IsSingletonAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, SingletonAttributeName);
        }

        public static bool IsProcessedAssemblyAttribute(this CustomAttribute attribute)
        {
            return Is(attribute, ProcessedAssemblyAttributeName);
        }

        public static string GetNamedAttributeName(this MethodReturnType returnType)
        {
            var attr = returnType.GetNamedAttribute();
            return attr == null ? null : attr.ConstructorArguments[0].Value as string;
        }

        public static string GetNamedAttributeName(this ICustomAttributeProvider parameterDefinition)
        {
            var attr = parameterDefinition.GetNamedAttribute();
            return attr == null ? null : attr.ConstructorArguments[0].Value as string;
        }

        public static CustomAttribute GetNamedAttribute(this MethodReturnType returnType)
        {
            return returnType.ExtractCustomAttribute(IsNamedAttribute);
        }

        public static CustomAttribute GetNamedAttribute(this ICustomAttributeProvider parameterDefinition)
        {
            return parameterDefinition.ExtractCustomAttribute(IsNamedAttribute);
        }

        private static bool Is(CustomAttribute attribute, string name)
        {
            if (attribute == null)
            {
                return false;
            }

            return attribute.AttributeType.FullName.Equals(name, StringComparison.Ordinal);
        }

        private static CustomAttribute ExtractCustomAttribute(this ICustomAttributeProvider parameterDefinition, Func<CustomAttribute, bool> predicate)
        {
            if (!parameterDefinition.HasCustomAttributes)
            {
                return null;
            }

            return parameterDefinition.CustomAttributes.FirstOrDefault(predicate);
        }

        private static CustomAttribute ExtractCustomAttribute(this MethodReturnType returnType, Func<CustomAttribute, bool> predicate)
        {
            if (!returnType.HasCustomAttributes)
            {
                return null;
            }

            return returnType.CustomAttributes.FirstOrDefault(predicate);
        }
    }
}
