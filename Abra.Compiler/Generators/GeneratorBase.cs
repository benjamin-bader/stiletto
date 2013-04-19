using System;
using System.IO;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public abstract class GeneratorBase
    {
        private readonly ITypeDefinition type;
        private readonly string literal;

        public string Namespace
        {
            get { return type.Namespace; }
        }

        public string Name
        {
            get { return type.Name; }
        }

        public string LiteralName
        {
            get { return literal; }
        }

        public string FullName
        {
            get { return type.FullName; }
        }

        public string AccessModifier
        {
            get { return CodeHelpers.AccessibilityName(type); }
        }

        public string Typeof
        {
            get { return "typeof(" + Name + ")"; }
        }

        public ITypeDefinition Type
        {
            get { return type; }
        }

        protected GeneratorBase(ITypeDefinition type)
        {
            if (type == null) {
                throw new ArgumentNullException("type");
            }
            this.type = type;
            this.literal = CodeHelpers.ToCodeLiteral(type);
        }

        public abstract void Configure(ErrorReporter errorReporter);

        public abstract void Generate(TextWriter output, Compiler compiler);
    }
}
