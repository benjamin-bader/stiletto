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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stiletto.Fody
{
    public static class EnumerableExtensions
    {
        public static ISet<T> ToSet<T>(this IEnumerable<T> collection, IEqualityComparer<T> comparer = null)
        {
            return comparer == null
                ? new HashSet<T>(collection)
                : new HashSet<T>(collection, comparer);
        }
    }
}
