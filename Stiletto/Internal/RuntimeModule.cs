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

namespace Stiletto.Internal
{
    public abstract class RuntimeModule
    {
        private readonly Type moduleType;
        private readonly string[] entryPoints;
        private readonly Type[] includes;
        private readonly bool complete;
        private readonly bool isLibrary;

        protected Type ModuleType
        {
            get { return moduleType; }
        }

        public string[] EntryPoints
        {
            get { return entryPoints; }
        }

        public Type[] Includes
        {
            get { return includes; }
        }

        public bool IsComplete
        {
            get { return complete; }
        }

        public bool IsLibrary
        {
            get { return isLibrary; }
        }

        public object Module { get; set; }

        protected RuntimeModule(Type moduleType, string[] entryPoints, Type[] includes, bool complete, bool isLibrary)
        {
            Conditions.CheckNotNull(moduleType, "moduleType");
            Conditions.CheckNotNull(entryPoints, "entryPoints");
            Conditions.CheckNotNull(includes, "includes");

            this.moduleType = moduleType;
            this.entryPoints = entryPoints;
            this.includes = includes;
            this.complete = complete;
            this.isLibrary = isLibrary;
        }

        public virtual void GetBindings(IDictionary<string, Binding> bindings)
        {
            
        }

        public abstract object CreateModule();
    }
}
