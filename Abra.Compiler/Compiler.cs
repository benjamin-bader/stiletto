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
        private readonly string pluginName;
        private readonly Queue<GeneratorBase> queue = new Queue<GeneratorBase>();

        private readonly IList<LazyBindingGenerator> lazyBindings = new List<LazyBindingGenerator>();
        private readonly IList<ProviderBindingGenerator> providerBindings = new List<ProviderBindingGenerator>();

        public Compiler(FileInfo projectFile, string pluginName)
        {
            this.pluginName = pluginName;

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

            var plugin = new PluginGenerator(allTypes.First(), pluginName, moduleGenerators, injectables, lazyBindings, providerBindings);
            plugin.Generate(output, this);
        }

        public void EnqueueLazyBinding(ITypeDefinition type, ITypeDefinition providedType, string key, string lazyKey)
        {
            var lazyBindingGenerator = new LazyBindingGenerator(type, providedType, key, lazyKey);
            lazyBindings.Add(lazyBindingGenerator);
            queue.Enqueue(lazyBindingGenerator);
        }

        public void EnqueueProviderBinding(ITypeDefinition type, ITypeDefinition providedType, string key, string providerKey)
        {
            var providerBindingGenerator = new ProviderBindingGenerator(type, providedType, key, providerKey);
            providerBindings.Add(providerBindingGenerator);
            queue.Enqueue(providerBindingGenerator);
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
