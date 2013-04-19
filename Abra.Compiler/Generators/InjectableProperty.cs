using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class InjectableProperty
    {
        public string Name { get; private set; }
        public IType Type { get; private set; }
        public string TypeName { get; private set; }
        public string Key { get; private set; }

        public InjectableProperty(IProperty prop)
        {
            var nameAttr = prop.Attributes.FirstOrDefault(a => a.AttributeType.FullName.Equals(Constants.NamedAttributeName));

            Name = prop.Name;
            Type = prop.ReturnType;
            TypeName = CompilerKeys.CompilerFriendlyName(prop.ReturnType);
            Key = CompilerKeys.ForTypeDef(Type, nameAttr != null
                ? (string) nameAttr.PositionalArguments[0].ConstantValue
                : null);
        }
    }
}