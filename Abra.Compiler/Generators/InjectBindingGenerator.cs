using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abra.Compiler.Templates;
using ICSharpCode.NRefactory.TypeSystem;

namespace Abra.Compiler.Generators
{
    internal class InjectBindingGenerator : GeneratorBase
    {
        public bool IsEntryPoint { get; private set; }
        public bool IsSingleton { get; private set; }
        public string Key { get; private set; }
        public string MemberKey { get; private set; }
        public IMethod InjectableConstructor { get; private set; }
        public IList<InjectableProperty> InjectableProperties { get; private set; }
        public IList<CtorParam> CtorParameters { get; private set; }
        public string BaseTypeKey { get; private set; }

        public InjectBindingGenerator(ITypeDefinition typedef, bool isEntryPoint)
            : base(typedef)
        {
            IsEntryPoint = isEntryPoint;    
        }

        public override void Configure(ErrorReporter errorReporter)
        {
            Key = CompilerKeys.ForTypeDef(Type);
            MemberKey = CompilerKeys.GetMemberKey(Type);
            IsSingleton = Type.Attributes.Any(Attributes.IsSingletonAttribute);

            if (!Type.IsPublicOrInternal()) {
                errorReporter.Error("Type {0} is marked [Inject], but is not visible.  Consider making it public or internal.", FullName);
            }

            var ctors = from c in Type.GetConstructors()
                        where c.Attributes.Any(Attributes.IsInjectAttribute)
                        select c;

            foreach (var ctor in ctors) {
                if (InjectableConstructor != null) {
                    errorReporter.Error("Type {0} has more than one [Inject] constructor.", FullName);
                    break;
                }

                if (!ctor.IsPublicOrInternal()) {
                    errorReporter.Error("{0}.{1} is marked [Inject] but is neither public nor internal.", FullName, ctor.ToString());
                }

                InjectableConstructor = ctor;
            }

            var props = from p in Type.GetProperties(options: GetMemberOptions.IgnoreInheritedMembers)
                        where p.Attributes.Any(Attributes.IsInjectAttribute)
                        select p;

            InjectableProperties = new List<InjectableProperty>();
            foreach (var prop in props) {
                if (!prop.CanSet) {
                    errorReporter.Error("{0}.{1} is marked [Inject], but does not have a setter.", FullName, prop.Name);
                    continue;
                }

                if (prop.Setter.Accessibility != Accessibility.Public
                    && prop.Setter.Accessibility != Accessibility.Internal
                    && prop.Setter.Accessibility != Accessibility.ProtectedOrInternal) {
                    errorReporter.Error("{0}.{1} is marked [Inject], but its setter is neither public nor internal.", FullName, prop.Name);
                    continue;
                }

                InjectableProperties.Add(new InjectableProperty(prop));
            }

            if (InjectableConstructor == null) {
                if (InjectableProperties.Count == 0 && !IsEntryPoint) {
                    errorReporter.Error("No injectable properties or constructors found on type: " + FullName);
                    return;
                }

                var defaultCtor = Type.GetConstructors(c => c.Parameters.Count == 0 &&
                                                            (c.IsPublic || c.IsInternal || c.IsProtectedAndInternal))
                                      .FirstOrDefault();

                if (defaultCtor == null) {
                    errorReporter.Error("Type {0} has no [Inject] constructor and no default constructor", FullName);
                    return;
                }

                InjectableConstructor = defaultCtor;
            }

            CtorParameters = InjectableConstructor
                .Parameters
                .Select(p => new CtorParam(p))
                .ToList();

            var supertype = Type.DirectBaseTypes.LastOrDefault();
            var supertypeDef = supertype != null ? supertype.GetDefinition() : null;

            if (supertypeDef == null ||
                supertypeDef.ParentAssembly == null ||
                supertypeDef.ParentAssembly.AssemblyName.StartsWith("System") ||
                supertypeDef.ParentAssembly.AssemblyName.StartsWith("Microsoft") ||
                supertypeDef.ParentAssembly.AssemblyName.StartsWith("mscorlib") ||
                supertypeDef.ParentAssembly.AssemblyName.StartsWith("Mono")) {
                BaseTypeKey = null;
            } else {
                BaseTypeKey = CompilerKeys.ForTypeDef(supertype);
            }
        }

        public override void Generate(TextWriter writer, Compiler compiler)
        {
            var template = new InjectBinding();
            template.Session = new Dictionary<string, object> {{"cls", this}};
            template.Initialize();
            writer.Write(template.TransformText());

            var dependencies = CtorParameters.Select(p => new {p.Key, p.Type})
                                             .Concat(InjectableProperties.Select(p => new {p.Key, p.Type}));

            // TODO: Consider created a "register-dependencies" phase between configuration and generation?
            //       This defers some theoretical validation until after output has begun, which is not great.
            //       Not urgent as currently Lazy and IProvider bindings have no configuration.
            foreach (var param in dependencies) {
                var key = CompilerKeys.GetProviderKey(param.Key);
                if (key != null) {
                    var innerType = ExtractSingleTypeArgument(param.Type);
                    compiler.EnqueueProviderBinding(param.Type.GetDefinition(), innerType);
                    continue;
                }

                key = CompilerKeys.GetLazyKey(param.Key);
                if (key != null) {
                    var innerType = ExtractSingleTypeArgument(param.Type);
                    compiler.EnqueueLazyBinding(param.Type.GetDefinition(), innerType);
                    continue;
                }
            }
        }

        private static ITypeDefinition ExtractSingleTypeArgument(IType type)
        {
            var parameterizedType = (ParameterizedType) type;
            return parameterizedType.TypeArguments[0].GetDefinition();
        }
    }
}