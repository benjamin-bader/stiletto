using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abra.Compiler.Reflection;
using Abra.Compiler.Templates;
using Abra.Internal.Plugins.Codegen;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    public class ModuleGenerator : GeneratorBase
    {
        private readonly ReflectedModule reflectedModule;

        public override string GeneratedClassName
        {
            get { return LiteralName + CodegenPlugin.ModuleSuffix; }
        }

        public IList<string> IncludedTypeofs { get; private set; }
        public IList<string> EntryPointKeys { get; private set; }
        public IList<ProviderMethod> ProviderMethods { get; private set; }
        public bool IsComplete { get; private set; }

        public ModuleGenerator(ReflectedModule reflectedModule)
            : base(reflectedModule.ModuleType)
        {
            this.reflectedModule = reflectedModule;
        }

        public override void Configure(ErrorReporter errorReporter)
        {
            IsComplete = reflectedModule.IsComplete;

            IncludedTypeofs = reflectedModule.IncludedModules.Select(t => "typeof(" + t.FullName + ")").ToList();
            EntryPointKeys = reflectedModule.EntryPoints.Select(CompilerKeys.GetMemberKey).ToList();

            if (!Type.IsPublicOrInternal()) {
                errorReporter.Error("Module type {0} is neither public nor internal.", FullName);
            }

            var methods = Type.GetMethods(options: GetMemberOptions.IgnoreInheritedMembers)
                              .Where(m => m.Attributes.Any(Attributes.IsProvidesAttribute));

            ProviderMethods = new List<ProviderMethod>();
            foreach (var method in methods) {
                if (!method.IsPublicOrInternal()) {
                    errorReporter.Error("{0} is marked [Provides] but is neither public nor internal.", method.FullName);
                    continue;
                }

                ProviderMethods.Add(new ProviderMethod(method));
            }

            if (IsComplete) {
                var providedKeys = new HashSet<string>(ProviderMethods.Select(m => m.Key));
                foreach (var method in ProviderMethods) {
                    if (!method.HasParams) {
                        continue;
                    }

                    foreach (var param in method.Params) {
                        if (!providedKeys.Contains(param.Key)) {
                            errorReporter.Error(
                                "{0} is marked as complete, but provider method '{1}' has an unsatisfied dependency on {2}.  Consider marking it [Module(IsComplete = false)].",
                                FullName,
                                method.Name,
                                param.TypeName);
                        }
                    }
                }
            }
        }

        public override void Generate(TextWriter output, Compiler compiler)
        {
            var generator = new RuntimeModule();
            generator.Session = new Dictionary<string, object> { { "mod", this } };
            generator.Initialize();
            output.WriteLine(generator.TransformText());
        }
    }
}
