using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abra.Compiler.Generators;
using Abra.Compiler.Reflection;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler
{
    public class Compiler
    {
        private readonly IList<ITypeDefinition> allTypes; 
        private readonly Queue<GeneratorBase> queue = new Queue<GeneratorBase>();

        public Compiler(FileInfo projectFile)
        {
            var sln = new Solution(projectFile.DirectoryName ?? Environment.CurrentDirectory);
            sln.AddProject(projectFile.FullName);
            sln.CreateCompilation();

            var mainProject = sln.Projects.Single(p => p.FileName == projectFile.FullName);

            allTypes = mainProject
                .Compilation
                .GetAllTypeDefinitions()
                .Where(t => t.ParentAssembly.AssemblyName.Equals(mainProject.AssemblyName))
                .ToList();
        }

        public void Compile(TextWriter output, ErrorReporter reporter)
        {
            var modules = new List<ReflectedModule>();
            var injectables = new List<InjectBindingGenerator>();

            foreach (var t in allTypes) {
                if (IsModule(t)) {
                    modules.Add(new ReflectedModule(t));
                } else if (IsInjectable(t)) {
                    var injectBindingGenerator = new InjectBindingGenerator(t, false);
                    injectBindingGenerator.Configure(reporter);
                    injectables.Add(injectBindingGenerator);
                }
            }

            var entryPoints = new List<GeneratorBase>();
            foreach (var entryPoint in modules.SelectMany(m => m.EntryPoints)) {
                var binding = new InjectBindingGenerator(entryPoint, true);
                binding.Configure(reporter);
                entryPoints.Add(binding);
            }

            var moduleGenerators = new List<ModuleGenerator>();
            foreach (var module in modules) {
                var moduleGenerator = new ModuleGenerator(module);
                moduleGenerator.Configure(reporter);
                moduleGenerators.Add(moduleGenerator);
            }

            if (!reporter.IsValid) {
                return;
            }

            foreach (var module in moduleGenerators) {
                queue.Enqueue(module);
            }

            foreach (var entryPoint in entryPoints) {
                queue.Enqueue(entryPoint);
            }

            foreach (var injectable in injectables) {
                queue.Enqueue(injectable);
            }

            var generatedBindingTypes = new HashSet<IType>();
            while (queue.Count > 0) {
                var gen = queue.Dequeue();
                if (!generatedBindingTypes.Add(gen.Type)) {
                    continue;
                }

                gen.Generate(output, this);
            }
        }

        public void EnqueueLazyBinding(ITypeDefinition type, ITypeDefinition providedType)
        {
            queue.Enqueue(new LazyBindingGenerator(type, providedType));
        }

        public void EnqueueProviderBinding(ITypeDefinition type, ITypeDefinition providedType)
        {
            queue.Enqueue(new ProviderBindingGenerator(type, providedType));
        }

        private static bool IsModule(ITypeDefinition type)
        {
            return type.Attributes.Any(Attributes.IsModuleAttribute);
        }

        private static bool IsInjectable(ITypeDefinition type)
        {
            var cs = type.GetConstructors();
            var ps = type.GetProperties(options: GetMemberOptions.IgnoreInheritedMembers);

            return cs.Any(c => c.Attributes.Any(Attributes.IsInjectAttribute))
                || ps.Any(p => p.Attributes.Any(Attributes.IsInjectAttribute));
        }
    }
}
