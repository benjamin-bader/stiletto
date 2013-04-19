using System.Collections.Generic;
using System.IO;
using Abra.Compiler.Templates;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class ProviderBindingGenerator : GeneratorBase
    {
        private readonly ITypeDefinition providedType;

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

        public ProviderBindingGenerator(ITypeDefinition type, ITypeDefinition providedType)
            : base(type)
        {
            this.providedType = providedType;
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
