using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Reflection
{
    public class ReflectedModule
    {
        private const string EntryPointName = "EntryPoints";
        private const string IncludedModulesName = "IncludedModules";
        private const string IsCompleteName = "IsComplete";

        private readonly ITypeDefinition moduleType;
        private readonly IList<ITypeDefinition> includedModules = new List<ITypeDefinition>();
        private readonly IList<ITypeDefinition> entryPoints = new List<ITypeDefinition>();
        private readonly bool isComplete;

        public ITypeDefinition ModuleType { get { return moduleType; } }
        public IList<ITypeDefinition> IncludedModules { get { return includedModules; } }
        public IList<ITypeDefinition> EntryPoints { get { return entryPoints; } }
        public bool IsComplete { get { return isComplete; } }

        public ReflectedModule(ITypeDefinition moduleType)
        {
            this.moduleType = moduleType;

            // If only we could see default values here.  Can we?  Probably not without the syntax tree... ugh.
            isComplete = true;

            var attr = moduleType.Attributes.FirstOrDefault(Attributes.IsModuleAttribute);

            if (attr == null) {
                throw new ArgumentException("Not a module!");
            }

            foreach (var namedArgument in attr.NamedArguments) {
                if (namedArgument.Key.Name.Equals(IsCompleteName, StringComparison.Ordinal)) {
                    var boolResult = (ConstantResolveResult) namedArgument.Value;
                    isComplete = (bool) boolResult.ConstantValue;
                    continue;
                }

                IList<ITypeDefinition> addTo = null;

                if (namedArgument.Key.Name.Equals(EntryPointName, StringComparison.Ordinal)) {
                    addTo = entryPoints;
                } else if (namedArgument.Key.Name.Equals(IncludedModulesName, StringComparison.Ordinal)) {
                    addTo = includedModules;
                }

                if (addTo == null) {
                    continue;
                }

                var arrayCreateResolveResult = (ArrayCreateResolveResult)namedArgument.Value;
                var childResults = arrayCreateResolveResult.GetChildResults().OfType<TypeOfResolveResult>();
                foreach (var result in childResults) {
                    addTo.Add(result.ReferencedType.GetDefinition());
                }
            }

            isComplete = isComplete && IncludedModules.Count == 0;
        }
    }
}
