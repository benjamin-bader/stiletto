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

namespace Stiletto.Internal
{
    public abstract class ProviderMethodBindingBase : Binding
    {
        private readonly string moduleName;
        private readonly string methodName;

        public string ProviderMethodName
        {
            get { return moduleName + "." + methodName; }
        }

        public ProviderMethodBindingBase(
            string providerKey, string membersKey, bool isSingleton, object requiredBy,
            string moduleName, string methodName)
            : base(providerKey, membersKey, isSingleton, requiredBy)
        {
            this.moduleName = moduleName;
            this.methodName = methodName;
        }
    }
}
