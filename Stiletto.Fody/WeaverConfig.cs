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

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Stiletto.Fody
{
    public class WeaverConfig
    {
        public bool SuppressUnusedBindingErrors { get; private set; }

        public Trie ExcludedClassPatterns { get; private set; }

        private WeaverConfig()
        {
        }

        public static WeaverConfig Load(XElement config)
        {
            var noUnusedBindingErrs = (bool?) config.Attribute("SuppressUnusedBindingsErrors")
                                   ?? (bool?) config.Element("SuppressUnusedBindingsErrors");

            var excludedClassElement = config.Element("ExcludeClasses");

            var excludedClasses = new List<string>();
            if (excludedClassElement != null)
            {
                var classes = from c in excludedClassElement.Elements("Class")
                              select (string) c;

                excludedClasses.AddRange(classes);
            }

            return new WeaverConfig
                   {
                       SuppressUnusedBindingErrors = noUnusedBindingErrs ?? false,
                       ExcludedClassPatterns = new Trie(excludedClasses),
                   };
        }
    }
}
