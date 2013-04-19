// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
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
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Abra.Compiler
{
    /// <summary>
    /// Represents a C# project (.csproj file)
    /// </summary>
    public class CSharpProject
    {
        private readonly string assemblyName;
        private readonly string fileName;
        private readonly IList<CSharpFile> files = new List<CSharpFile>();
        private readonly CompilerSettings compilerSettings = new CompilerSettings();
        private readonly IProjectContent projectContent;

        /// <summary>
        /// Name of the output assembly.
        /// </summary>
        public string AssemblyName
        {
            get { return assemblyName; }
        }

        public string FileName
        {
            get { return fileName; }
        }

        /// <summary>
        /// Full path to the .csproj file.
        /// </summary>
        public IList<CSharpFile> Files
        {
            get { return files; }
        }

        public CompilerSettings CompilerSettings
        {
            get { return compilerSettings; }
        }

        /// <summary>
        /// The unresolved type system for this project.
        /// </summary>
        public IProjectContent ProjectContent
        {
            get { return projectContent; }
        }

        /// <summary>
        /// The resolved type system for this project.
        /// This field is initialized once all projects have been loaded (in Solution constructor).
        /// </summary>
        public ICompilation Compilation { get; set; }

        public CSharpProject(Solution solution, string fileName)
        {
            // Normalize the file name
            fileName = Path.GetFullPath(fileName);

            this.fileName = fileName;

            // Use MSBuild to open the .csproj
            var msbuildProject = new Project(fileName);
            // Figure out some compiler settings
            assemblyName = msbuildProject.GetPropertyValue("AssemblyName");
            compilerSettings.AllowUnsafeBlocks = GetBoolProperty(msbuildProject, "AllowUnsafeBlocks");
            compilerSettings.CheckForOverflow = GetBoolProperty(msbuildProject, "CheckForOverflowUnderflow");
            
            var defineConstants = msbuildProject.GetPropertyValue("DefineConstants");
            foreach (var symbol in defineConstants.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)) {
                compilerSettings.ConditionalSymbols.Add(symbol.Trim());
            }

            // Initialize the unresolved type system
            IProjectContent pc = new CSharpProjectContent();
            pc = pc.SetAssemblyName(assemblyName);
            pc = pc.SetProjectFileName(fileName);
            pc = pc.SetCompilerSettings(compilerSettings);

            // Parse the C# code files
            foreach (var item in msbuildProject.GetItems("Compile"))
            {
                var file = new CSharpFile(this, Path.Combine(msbuildProject.DirectoryPath, item.EvaluatedInclude));
                files.Add(file);
            }
            // Add parsed files to the type system
            pc = pc.AddOrUpdateFiles(files.Select(f => f.UnresolvedTypeSystemForFile));

            // Add referenced assemblies:
            pc = ResolveAssemblyReferences(msbuildProject)
                .Select(solution.LoadAssembly)
                .Aggregate(pc, (current, assembly) => current.AddAssemblyReferences(assembly));

            foreach (var projectReference in msbuildProject.GetItems("ProjectReference")) {
                var filePath = Path.GetFullPath(Path.Combine(msbuildProject.DirectoryPath, projectReference.EvaluatedInclude));
                var projectName = projectReference.GetMetadataValue("Name");
                pc = pc.AddAssemblyReferences(new ProjectReference(filePath));
                solution.AddProject(filePath);
            }

            // Add project references:
            pc = msbuildProject.GetItems("ProjectReference")
                .Select(item => Path.Combine(msbuildProject.DirectoryPath, item.EvaluatedInclude))
                .Select(Path.GetFullPath)
                .Aggregate(pc, (current, referencedFileName) => current.AddAssemblyReferences(new ProjectReference(referencedFileName)));

            projectContent = pc;
        }

        private IEnumerable<string> ResolveAssemblyReferences(Project project)
        {
            // Use MSBuild to figure out the full path of the referenced assemblies
            var projectInstance = project.CreateProjectInstance();
            projectInstance.SetProperty("BuildingProject", "false");
            project.SetProperty("DesignTimeBuild", "true");
            projectInstance.Build("ResolveAssemblyReferences", new[] { new ConsoleLogger(LoggerVerbosity.Minimal) });
            
            var items = projectInstance.GetItems("_ResolveAssemblyReferenceResolvedFiles");
            var baseDirectory = Path.GetDirectoryName(fileName) ?? string.Empty;
            return items.Select(i => Path.Combine(baseDirectory, i.GetMetadataValue("Identity")));
        }

        private static bool GetBoolProperty(Project project, string name, bool defaultValue = false)
        {
            var text = project.GetPropertyValue(name);

            bool parsedValue;
            return bool.TryParse(text, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        public override string ToString()
        {
            return string.Format("[CSharpProject AssemblyName={0}]", assemblyName);
        }
    }
}
