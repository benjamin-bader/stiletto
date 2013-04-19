using System;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class ProviderMethodParam
    {
        public string Name { get; private set; }
        public string TypeName { get; private set; }
        public string Key { get; private set; }
        public IType ParamType { get; private set; }

        public ProviderMethodParam(IParameter parameter)
        {
            Name = parameter.Name;

            var qualifierName = parameter
                .Attributes
                .Where(Attributes.IsNamedAttribute)
                .Select(a => a.PositionalArguments[0].ConstantValue)
                .Cast<string>()
                .FirstOrDefault();

            Key = CompilerKeys.ForTypeDef(parameter.Type, qualifierName);
            TypeName = CodeHelpers.ToCodeLiteral(parameter.Type.GetDefinition());
            ParamType = parameter.Type;
        }
    }
}
