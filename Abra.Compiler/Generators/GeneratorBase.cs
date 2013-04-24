using System;
using System.IO;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public abstract class GeneratorBase
    {
        private readonly ITypeDefinition type;
        private readonly string literal;

        public abstract string GeneratedClassName { get; }

        public virtual string Namespace
        {
            get { return type.Namespace; }
        }

        public virtual string Name
        {
            get { return type.Name; }
        }

        public virtual string LiteralName
        {
            get { return literal; }
        }

        public virtual string FullName
        {
            get { return type.FullName; }
        }

        public virtual string AccessModifier
        {
            get { return CodeHelpers.AccessibilityName(type); }
        }

        public virtual string Typeof
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

        public static string BindingName(string name)
        {
            return name.Replace(".", "_");
        }
    }
}
