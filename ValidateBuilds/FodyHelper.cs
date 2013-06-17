using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using NLog;
using Stiletto.Fody;

namespace ValidateBuilds
{
    public class FodyHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string projectPath;
        private readonly string assemblyPath;

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
            logger.Debug("Reading assembly {0}...", assemblyPath);
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);

            logger.Debug("Assembly read.  Processing...");
            var moduleWeaver = new ModuleWeaver
                               {
                                   AssemblyFilePath = assemblyPath,
                                   AssemblyResolver = CreateAssemblyResolver(),
                                   Config = CreateFodyConfig(),
                                   LogError = message => Errors.Add(message),
                                   LogWarning = message => Warnings.Add(message),
                                   ProjectDirectoryPath = projectPath,
                                   ReferenceCopyLocalPaths = GetCopyLocalAssemblies(assemblyPath),
                                   ModuleDefinition = assemblyDefinition.MainModule
                               };

            try
            {
                moduleWeaver.Execute();
            }
            catch (Exception ex)
            {
                logger.DebugException("Fody processing failed for assembly at " + assemblyPath, ex);
            }

            return assemblyDefinition.MainModule;
        }

        private IAssemblyResolver CreateAssemblyResolver()
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.ResolveFailure += OnAssemblyResolveFailed;
            return resolver;
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

        private XElement CreateFodyConfig()
        {
            return new XElement("Stiletto", new XAttribute("SuppressGraphviz", true));
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
