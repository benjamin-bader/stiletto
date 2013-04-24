using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Abra.Compiler.Templates;
using Abra.Internal;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    /// <summary>
    /// Generates an <see cref="IPlugin"/> implementation containing references
    /// to all other generated code.  There should only ever be one of these
    /// generated, as currently they have a hardcoded name.
    /// </summary>
    public class PluginGenerator : GeneratorBase
    {
        private readonly string pluginNamespace;
        private readonly string pluginName;

        public override string GeneratedClassName
        {
            get { return pluginName; }
        }

        public string RootNamespace
        {
            get { return pluginNamespace ?? Type.Namespace; }
        }

        public IList<KeyedClass> Modules { get; private set; }
        public IList<KeyedClass> InjectBindings { get; private set; }
        public IList<KeyedClass> LazyBindings { get; private set; }
        public IList<KeyedClass> ProvidesBindings { get; private set; }

        public PluginGenerator(
            ITypeDefinition type,
            string pluginName,
            IEnumerable<ModuleGenerator> modules,
            IEnumerable<InjectBindingGenerator> injectBindings,
            IEnumerable<LazyBindingGenerator> lazyBindings,
            IEnumerable<ProviderBindingGenerator> providesBindings) : base(type)
        {
            Modules = new List<KeyedClass>();
            InjectBindings = new List<KeyedClass>();
            LazyBindings = new List<KeyedClass>();
            ProvidesBindings = new List<KeyedClass>();

            var lastDot = pluginName.LastIndexOf('.');
            if (lastDot < 0) {
                this.pluginName = pluginName;
            } else {
                this.pluginNamespace = pluginName.Substring(0, lastDot);
                this.pluginName = pluginName.Substring(lastDot + 1);
            }

            foreach (var m in modules) {
                // Modules are looked up by their original type name, not their generated wrapper name.
                var key = m.Namespace + "." + m.LiteralName;
                var className = m.Namespace + "." + BindingName(m.GeneratedClassName);
                Modules.Add(new KeyedClass(key, className));
            }

            foreach (var i in injectBindings) {
                var className = i.Namespace + "." + BindingName(i.GeneratedClassName);
                InjectBindings.Add(new KeyedClass(i.Key, className));
                InjectBindings.Add(new KeyedClass(i.MemberKey, className));
            }

            foreach (var l in lazyBindings) {
                var key = l.Key;
                var className = l.ProvidedTypeNamespace + "." + BindingName(l.GeneratedClassName);
                LazyBindings.Add(new KeyedClass(key, className));
            }

            foreach (var p in providesBindings) {
                var key = p.Key;
                var className = p.ProvidedTypeNamespace + "." + BindingName(p.GeneratedClassName);
                ProvidesBindings.Add(new KeyedClass(key, className));
            }
        }

        public override void Configure(ErrorReporter errorReporter)
        {
            // No-op
        }

        public override void Generate(TextWriter output, Compiler compiler)
        {
            var generator = new CompiledPlugin();
            generator.Session = new Dictionary<string, object> {{"cls", this}};
            generator.Initialize();
            output.WriteLine(generator.TransformText());
        }

        public class KeyedClass
        {
            public string Key { get; private set; }
            public string ClassName { get; private set; }

            public KeyedClass(string key, string className)
            {
                Key = key;
                ClassName = className;
            }
        }
    }
}
