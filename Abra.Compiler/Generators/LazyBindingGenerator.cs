using System.Collections.Generic;
using System.IO;
using Abra.Compiler.Templates;
using Abra.Internal.Plugins.Codegen;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class LazyBindingGenerator : GeneratorBase
    {
        private readonly ITypeDefinition providedType;
        private readonly string key;
        private readonly string lazyKey;

        public override string GeneratedClassName
        {
            get { return ProvidedTypeLiteralName + CodegenPlugin.LazySuffix; }
        }

        public string ProvidedTypeNamespace
        {
            get { return providedType.Namespace; }
        }

        public string ProvidedTypeName
        {
            get { return providedType.Name; }
        }

        public string ProvidedTypeFullName
        {
            get { return providedType.FullName; }
        }

        public string ProvidedTypeLiteralName
        {
            get { return CodeHelpers.ToCodeLiteral(providedType); }
        }

        public string Key
        {
            get { return key; }
        }

        public string LazyKey
        {
            get { return lazyKey; }
        }

        public LazyBindingGenerator(ITypeDefinition type, ITypeDefinition providedType, string key, string lazyKey)
            : base(type)
        {
            this.providedType = providedType;
            this.key = key;
            this.lazyKey = lazyKey;
        }

        public override void Configure(ErrorReporter errorReporter)
        {
        }

        public override void Generate(TextWriter output, Compiler compiler)
        {
            var generator = new LazyBinding();
            generator.Session = new Dictionary<string, object> { { "cls", this } };
            generator.Initialize();
            output.WriteLine(generator.TransformText());
        }
    }
}
