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

namespace Stiletto.Fody.Generators
{
    public class InjectMemberInfo
    {
        private readonly string key;
        private readonly string lazyKey;
        private readonly string providerKey;
        private readonly string memberName;
        private readonly TypeReference type;

        public string Key
        {
            get { return key; }
        }

        public bool HasLazyKey
        {
            get { return lazyKey != null; }
        }

        public bool HasProviderKey
        {
            get { return providerKey != null; }
        }

        public string LazyKey
        {
            get { return lazyKey; }
        }

        public string ProviderKey
        {
            get { return providerKey; }
        }

        public string MemberName
        {
            get { return memberName; }
        }

        public TypeReference Type
        {
            get { return type; }
        }

        private InjectMemberInfo(string key, TypeReference type)
        {
            this.key = key;
            this.type = type;
            lazyKey = CompilerKeys.GetLazyKey(key);
            providerKey = CompilerKeys.GetProviderKey(key);
        }

        public InjectMemberInfo(ParameterDefinition param)
            : this(CompilerKeys.ForParam(param), param.ParameterType)
        {
            memberName = param.Name;
        }

        public InjectMemberInfo(PropertyDefinition property)
            : this(CompilerKeys.ForProperty(property), property.PropertyType)
        {
            memberName = property.FullName;
        }
    }
}
