using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class ProviderMethod
    {
        public string Name { get; private set; }
        public string BindingName { get; private set; }
        public string Key { get; private set; }
        public IList<ProviderMethodParam> Params { get; private set; }
        public IMethod Method { get; private set; }
        public bool IsSingleton { get; private set; }
        public IType ProvidedType { get; private set; }
        public bool HasParams
        {
            get { return Params != null && Params.Count > 0; }
        }

        public ProviderMethod(IMethod method)
        {
            Name = method.Name;
            Params = method.Parameters.Select(p => new ProviderMethodParam(p)).ToList();
            IsSingleton = method.Attributes.Any(Attributes.IsSingletonAttribute);
            Method = method;

            var qualifierName = method
                .Attributes
                .Where(Attributes.IsNamedAttribute)
                .Select(a => a.PositionalArguments[0].ConstantValue)
                .Cast<string>()
                .SingleOrDefault();

            Key = CompilerKeys.ForTypeDef(method.ReturnType, qualifierName);

            BindingName = Name + Params.Count + (qualifierName ?? "");
            ProvidedType = method.ReturnType;
        }
    }
}
