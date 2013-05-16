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

﻿using System.Collections.Generic;

namespace Abra.Internal
{
    internal class SingletonBinding : Binding
    {
        private readonly Binding binding;
        private object instance;
        private bool initialized;

        public override bool IsResolved
        {
            get { return binding.IsResolved; }
            set { binding.IsResolved = value; }
        }

        public override bool IsCycleFree
        {
            get { return binding.IsCycleFree; }
            set { binding.IsCycleFree = value; }
        }

        public override bool IsVisiting
        {
            get { return binding.IsVisiting; }
            set { binding.IsVisiting = value; }
        }

        public override bool IsLibrary
        {
            get { return binding.IsLibrary; }
            set { binding.IsLibrary = value; }
        }

        internal SingletonBinding(Binding binding)
            : base(binding.ProviderKey, binding.MembersKey, true, binding.RequiredBy)
        {
            this.binding = binding;
        }

        public override void Resolve(Resolver resolver)
        {
            binding.Resolve(resolver);
        }

        public override object Get()
        {
            if (!initialized)
            {
                lock (this)
                {
                    instance = binding.Get();
                    initialized = true;
                }
            }

            return instance;
        }

        public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            binding.GetDependencies(injectDependencies, propertyDependencies);
        }
    }
}
