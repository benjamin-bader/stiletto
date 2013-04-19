using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class CtorParam
    {
        public string Name { get; private set; }
        public IType Type { get; private set; }
        public string TypeName { get; private set; }
        public string Key { get; private set; }

        public CtorParam(IParameter param)
        {
            Type = param.Type;
            TypeName = CompilerKeys.CompilerFriendlyName(Type);
            var nameAttr = param.Attributes.FirstOrDefault(a => a.AttributeType.FullName.Equals(Constants.NamedAttributeName));
            if (nameAttr != null) {
                Name = (string) nameAttr.PositionalArguments[0].ConstantValue;
            }
            Key = CompilerKeys.ForTypeDef(Type, Name);
        }
    }
}