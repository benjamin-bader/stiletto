// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
// Copyright (c) Ben Bader
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Utils;

namespace Abra.Compiler
{
    public class Solution
    {
        private readonly string directory;
        private readonly IList<CSharpProject> projects = new List<CSharpProject>();
        private readonly IList<ICompilation> compilations = new List<ICompilation>();
        private readonly ISet<string> includedProjectFiles = new HashSet<string>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, IUnresolvedAssembly> assemblies =
            new ConcurrentDictionary<string, IUnresolvedAssembly>(Platform.FileNameComparer);

        private bool hasCompiled;

        public IEnumerable<CSharpFile> AllFiles
        {
            get { return Projects.SelectMany(p => p.Files); }
        }

        public IList<CSharpProject> Projects
        {
            get { return projects; }
        }

        public IList<ICompilation> Compilations
        {
            get
            {
                if (!hasCompiled) {
                    CreateCompilation();
                }

                return compilations;
            }
        }

        public string Directory
        {
            get { return directory; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory">
        /// The root directory of the solution, used to resolve relative
        /// paths in project references.
        /// </param>
        public Solution(string directory)
        {
            if (string.IsNullOrEmpty(directory)) {
                throw new ArgumentNullException("directory");
            }

            if (!System.IO.Directory.Exists(directory)) {
                throw new ArgumentException("Non-existing solution path: " + directory, "directory");
            }

            this.directory = directory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">
        /// The path to the project file being added, relative to the solution's
        /// root path.
        /// </param>
        public void AddProject(string path)
        {
            var location = Path.IsPathRooted(path) ? path : Path.Combine(Directory, path);
            if (!includedProjectFiles.Add(location)) {
                return;
            }
            var project = new CSharpProject(this, location);
            Projects.Add(project);
        }

        /// <summary>
        /// Resolves all types in all of the included projects.
        /// </summary>
        public void CreateCompilation()
        {
            var snapshot = new DefaultSolutionSnapshot(Projects.Select(p => p.ProjectContent));
            
            foreach (var p in projects) {
                var compilation = snapshot.GetCompilation(p.ProjectContent);
                compilations.Add(compilation);
                p.Compilation = compilation;
            }

            hasCompiled = true;
        }

        /// <summary>
        /// Loads a referenced assembly from a .dll.
        /// Returns the existing instance if the assembly was already loaded.
        /// </summary>
        public IUnresolvedAssembly LoadAssembly(string assemblyFileName)
        {
            return assemblies.GetOrAdd(assemblyFileName, file => new CecilLoader().LoadAssemblyFile(file));
        }
    }
}
