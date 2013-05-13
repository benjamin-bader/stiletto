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
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Abra.Fody
{
    public static class MethodReferenceExtensions
    {
        public static bool AreSame(this MethodReference method, MethodReference other)
        {
            var possiblyEqual =
                method.DeclaringType.FullName.Equals(other.DeclaringType.FullName, StringComparison.Ordinal)
                && method.FullName.Equals(other.FullName, StringComparison.Ordinal)
                && method.Parameters.Count == other.Parameters.Count
                && method.GenericParameters.Count == other.GenericParameters.Count
                && method.IsGenericInstance == other.IsGenericInstance;

            if (!possiblyEqual) {
                return false;
            }

            for (var i = 0; i < method.Parameters.Count; ++i) {
                var pThis = method.Parameters[i];
                var pThat = other.Parameters[i];

                if (!pThis.ParameterType.FullName.Equals(pThat.ParameterType.FullName, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        public static MethodReference MakeHostInstanceGeneric(this MethodReference reference, params TypeReference[] args)
        {
            var instance = new MethodReference(
                reference.Name,
                reference.ReturnType,
                reference.DeclaringType.MakeGenericInstanceType(args))
            {
                HasThis = reference.HasThis,
                ExplicitThis = reference.ExplicitThis,
                CallingConvention = reference.CallingConvention
            };

            foreach (var parameter in reference.Parameters) {
                instance.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }

            foreach (var genericParam in reference.GenericParameters) {
                instance.GenericParameters.Add(new GenericParameter(genericParam.Name, instance));
            }

            return instance;
        }
    }
}
