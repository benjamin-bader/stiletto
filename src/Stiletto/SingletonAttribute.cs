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

namespace Stiletto
{
    /// <summary>
    /// Marks a class or a provider method as a singleton.  When dependencies
    /// are being resolved, at most one instance of the marked class or provider
    /// will be created.  The new instance or return value will be cached and used
    /// to satisfy all subsequent dependencies on this type.
    /// </summary>
    [Qualifier]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, Inherited = false)]
    public class SingletonAttribute : Attribute
    {
    }
}
