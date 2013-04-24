using System.Collections.Generic;
using System.IO;
using Abra.Compiler.Templates;
using Abra.Internal.Plugins.Codegen;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class ProviderBindingGenerator : GeneratorBase
    {
        private readonly ITypeDefinition providedType;
        private readonly string key;
        private readonly string providesKey;

        public override string GeneratedClassName
        {
            get { return ProvidedTypeLiteralName + CodegenPlugin.IProviderSuffix; }
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

        public string ProvidesKey
        {
            get { return providesKey; }
        }

        public ProviderBindingGenerator(ITypeDefinition type, ITypeDefinition providedType, string key, string providesKey)
            : base(type)
        {
            this.providedType = providedType;
            this.key = key;
            this.providesKey = providesKey;
        }

        public override void Configure(ErrorReporter errorReporter)
        {
        }

        public override void Generate(TextWriter output, Compiler compiler)
        {
            var generator = new ProviderBinding();
            generator.Session = new Dictionary<string, object> { { "cls", this } };
            generator.Initialize();
            output.WriteLine(generator.TransformText());
        }
    }
}
