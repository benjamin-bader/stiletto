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

namespace Abra.Internal
{
    public class Visitable
    {
        [Flags]
        public enum VisitingState : byte
        {
            IsVisiting = 1,
            IsCycleFree = 2
        }

        private VisitingState state;

        public virtual bool IsVisiting
        {
            get { return (state & VisitingState.IsVisiting) == VisitingState.IsVisiting; }
            set
            {
                state = value
                    ? (state | VisitingState.IsVisiting)
                    : (state & ~VisitingState.IsVisiting);
            }
        }

        public virtual bool IsCycleFree
        {
            get { return (state & VisitingState.IsCycleFree) == VisitingState.IsCycleFree; }
            set
            {
                state = value
                    ? (state | VisitingState.IsCycleFree)
                    : (state & ~VisitingState.IsCycleFree);
            }
        }
    }

    public static class VisitableWrapper
    {
        public static VisitableWrapper<T> Wrap<T>(T data)
        {
            return new VisitableWrapper<T>(data);
        }
    }

    public class VisitableWrapper<T> : Visitable
    {
        private readonly T data;

        public T Data
        {
            get { return data; }
        }

        public VisitableWrapper(T data)
        {
            this.data = data;
        }
    }

    internal static class WrapperExtensions
    {
        internal static VisitableWrapper<TResult> Select<TInput, TResult>(
            this VisitableWrapper<TInput> wrapper,
            Func<TInput, TResult> selector)
        {
            return new VisitableWrapper<TResult>(selector(wrapper.Data));
        }
    }
}
