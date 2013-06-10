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

using Mono.Cecil;

namespace Stiletto.Fody
{
    public static class TypeAttributesExtensions
    {
        public static bool IsVisible(this TypeDefinition type)
        {
            return IsVisible(type.Attributes);
        }

        public static bool IsVisible(this MethodDefinition method)
        {
            return IsVisible(method.Attributes);
        }

        public static bool IsVisible(this PropertyDefinition property)
        {
            return IsVisible(property.GetMethod) && IsVisible(property.SetMethod);
        }

        private static bool IsVisible(this TypeAttributes attrs)
        {
            switch (attrs & TypeAttributes.VisibilityMask)
            {
                case TypeAttributes.Public:
                case TypeAttributes.NestedAssembly:
                case TypeAttributes.NestedFamORAssem:
                case TypeAttributes.NestedPublic:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsVisible(this MethodAttributes attrs)
        {
            switch (attrs & MethodAttributes.MemberAccessMask)
            {
                case MethodAttributes.Public:
                case MethodAttributes.Assembly:
                case MethodAttributes.FamORAssem:
                    return true;

                default:
                    return false;
            }
        }
    }
}
