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

namespace Stiletto
{
    /// <summary>
    /// Represents an object which can provide a dependency of type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of dependency which can be provided.
    /// </typeparam>
    /// <remarks>
    /// A property or constructor parameter may be declared an
    /// <see cref="IProvider&lt;T&gt;"/> in order to, for example, break up
    /// a circular dependency.
    /// More-than-rare use of this mechanism is frequently a code smell.
    /// </remarks>
    public interface IProvider<out T>
    {
        /// <summary>
        /// Gets an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <returns>
        /// Returns an instance of type <typeparamref name="T"/>.
        /// </returns>
        T Get();
    }
}
