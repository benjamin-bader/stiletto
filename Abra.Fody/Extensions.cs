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
﻿using Mono.Cecil;

namespace Abra.Fody
{
    public static class Extensions
    {
        public static TResult Maybe<TInput, TResult>(this TInput input, Func<TInput, TResult> result)
            where TInput : class
            where TResult : class
        {
            if (input == null) {
                return null;
            }

            return result(input);
        }
    }
}
