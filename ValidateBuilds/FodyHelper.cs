using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using Stiletto.Fody;

namespace ValidateBuilds
{
    public class FodyHelper
    {
        private readonly string projectPath;
        private readonly string assemblyPath ;

        public IList<string> Errors { get; private set; }
        public IList<string> Warnings { get; private set; }
 
        public FodyHelper(string projectPath, string assemblyPath)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentNullException("projectPath");
            }

            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new ArgumentNullException("assemblyPath");
            }

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException(assemblyPath);
            }

            this.projectPath = projectPath;
            this.assemblyPath = assemblyPath;

            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public ModuleDefinition ProcessAssembly()
        {
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);
            var assemblyResolver = new DefaultAssemblyResolver();
            assemblyResolver.ResolveFailure += OnAssemblyResolveFailed;

            var moduleWeaver = new ModuleWeaver
                               {
                                   AssemblyFilePath = assemblyPath,
                                   AssemblyResolver = assemblyResolver,
                                   Config = new XElement("Stiletto"),
                                   LogError = message => Errors.Add(message),
                                   LogWarning = message => Warnings.Add(message),
                                   ProjectDirectoryPath = projectPath,
                                   ReferenceCopyLocalPaths = GetCopyLocalAssemblies(assemblyPath),
                                   ModuleDefinition = assemblyDefinition.MainModule
                               };

            moduleWeaver.Execute();

            return assemblyDefinition.MainModule;
        }

        private AssemblyDefinition OnAssemblyResolveFailed(object sender, AssemblyNameReference assemblyNameReference)
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var name = assemblyNameReference.Name;
            
            foreach (var ext in new[] {".dll", ".exe"})
            {
                var path = Path.Combine(assemblyDirectory, name + ext);
                if (!File.Exists(path))
                {
                    continue;
                }

                return AssemblyDefinition.ReadAssembly(path);
            }

            return null;
        }

        private List<string> GetCopyLocalAssemblies(string assemblyPath)
        {
            var canonicalAssemblyPath = Path.GetFullPath(assemblyPath);
            var deployDirectory = Path.GetDirectoryName(canonicalAssemblyPath);
            return Directory.EnumerateFiles(deployDirectory, "*.dll")
                            .Where(dll => dll != canonicalAssemblyPath)
                            .ToList();
        }
    }
}
