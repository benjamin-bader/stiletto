using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Abra.Fody.Generators
{
    public class ModuleGenerator : Generator
    {
        private readonly TypeDefinition moduleType;

        private IList<MethodDefinition> baseProvidesMethods;
        private MethodReference moduleCtor;

        public bool IsComplete { get; private set; }
        public bool IsOverride { get; private set; }
        public ISet<string> ProvidedKeys { get; private set; } 
        public IList<TypeReference> IncludedModules { get; private set; }
        public IList<TypeReference> EntryPoints { get; private set; }
        public IList<MethodDefinition> BaseProvidesMethods { get { return baseProvidesMethods; }}
        public IList<ProviderMethodBindingGenerator> ProviderGenerators { get; private set; } 

        private MethodReference generatedCtor;

        public ModuleGenerator(ModuleDefinition moduleDefinition, TypeDefinition moduleType)
            : base(moduleDefinition)
        {
            this.moduleType = Conditions.CheckNotNull(moduleType, "moduleType");;

            var attr = moduleType.CustomAttributes.SingleOrDefault(Attributes.IsModuleAttribute);

            if (attr == null) {
                throw new ArgumentException(moduleType.FullName + " is not marked as a [Module].", "moduleType");
            }

            CustomAttributeNamedArgument? argComplete = null,
                                          argEntryPoints = null,
                                          argIncludes = null,
                                          argOverrides = null;

            foreach (var arg in attr.Properties) {
                switch (arg.Name) {
                    case "IsComplete":
                        argComplete = arg;
                        break;
                    case "EntryPoints":
                        argEntryPoints = arg;
                        break;
                    case "IncludedModules":
                        argIncludes = arg;
                        break;
                    case "IsOverride":
                        argOverrides = arg;
                        break;
                    default:
                        throw new Exception("WTF, unexpected ModuleAttribute property: " + arg.Name);
                }
            }

            IsComplete = argComplete == null || (bool) argComplete.Value.Argument.Value;
            IsOverride = argOverrides != null && (bool) argOverrides.Value.Argument.Value;

            EntryPoints = new List<TypeReference>();
            if (argEntryPoints != null) {
                foreach (var val in (CustomAttributeArgument[]) argEntryPoints.Value.Argument.Value) {
                    var entryPointType = (TypeReference) val.Value;
                    EntryPoints.Add(entryPointType);
                }
            }

            IncludedModules = new List<TypeReference>();
            if (argIncludes != null) {
                foreach (var val in (CustomAttributeArgument[]) argIncludes.Value.Argument.Value) {
                    var includeType = (TypeReference) val.Value;
                    IncludedModules.Add(includeType);
                }
            }

            baseProvidesMethods = moduleType
                .Methods
                .Where(m => m.CustomAttributes.Any(Attributes.IsProvidesAttribute))
                .ToList();

            ProviderGenerators = baseProvidesMethods
                .Select(m => new ProviderMethodBindingGenerator(ModuleDefinition, moduleType, m))
                .ToList();
        }

        public override void Validate(IWeaver weaver)
        {
            if (moduleType.BaseType != null && moduleType.BaseType.FullName != ModuleDefinition.TypeSystem.Object.FullName) {
                weaver.LogError("Modules must inherit from System.Object");
            }

            if (moduleType.IsAbstract) {
                weaver.LogError("Modules cannot be abstract.");
            }

            moduleCtor = moduleType.GetConstructors().FirstOrDefault(m => m.Parameters.Count == 0);
            if (moduleCtor == null) {
                weaver.LogError(moduleType.FullName + " is marked as a [Module], but no default constructor is visible.");
            }

            if (IncludedModules.Count == 0 && baseProvidesMethods.Count == 0) {
                weaver.LogError("Modules must expose at least one [Provides] method.");
            }

            ProvidedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in baseProvidesMethods) {
                var name = method.GetNamedAttributeName();
                var key = CompilerKeys.ForType(method.ReturnType, name);

                if (!ProvidedKeys.Add(key)) {
                    weaver.LogError("Duplicate provider key for method " + moduleType.FullName + "." + method.Name);
                }
            }

            if (IsComplete) {
                foreach (var method in baseProvidesMethods) {
                    foreach (var param in method.Parameters) {
                        var name = param.GetNamedAttributeName();
                        var key = CompilerKeys.ForType(param.ParameterType, name);

                        if (!ProvidedKeys.Contains(key)) {
                            var msg = "Module type {0} is a complete module but has an unsatisfied dependency on {1}{2}";
                            var nameDescr = name == null ? string.Empty : "[Named(\"" + name + "\")] ";
                            weaver.LogError(string.Format(msg, moduleType.FullName, nameDescr, param.ParameterType.FullName));
                        }
                    }
                }
            }

            foreach (var gen in ProviderGenerators) {
                gen.Validate(weaver);
            }
        }

        public override TypeDefinition Generate(IWeaver weaver)
        {
            var name = moduleType.Name + Internal.Plugins.Codegen.CodegenPlugin.ModuleSuffix;
            var t = new TypeDefinition(moduleType.Namespace, name, moduleType.Attributes, References.RuntimeModule);

            t.CustomAttributes.Add(new CustomAttribute(References.CompilerGeneratedAttribute));

            foreach (var gen in ProviderGenerators) {
                gen.RuntimeModuleType = t;
                gen.Generate(weaver);
            }

            EmitCtor(t);
            EmitCreateModule(t);
            EmitGetBindings(t);

            if (moduleType.DeclaringType != null) {
                t.DeclaringType = moduleType.DeclaringType;
            }

            return t;
        }

        public override KeyedCtor GetKeyedCtor ()
        {
            // We don't care about keys for modules, we can dispatch on moduleType.
            return null;
        }

        public Tuple<TypeReference, MethodReference> GetModuleTypeAndGeneratedCtor()
        {
            Conditions.CheckNotNull(generatedCtor);
            return Tuple.Create((TypeReference) moduleType, generatedCtor);
        }

        private void EmitCreateModule(TypeDefinition runtimeModule)
        {
            var createModule = new MethodDefinition(
                "CreateModule",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Object);

            var il = createModule.Body.GetILProcessor();
            il.Emit(OpCodes.Newobj, moduleCtor);
            il.Emit(OpCodes.Ret);

            runtimeModule.Methods.Add(createModule);
        }

        private void EmitGetBindings(TypeDefinition runtimeModule)
        {
            var getBindings = new MethodDefinition(
                "GetBindings",
                MethodAttributes.Public | MethodAttributes.Virtual,
                ModuleDefinition.TypeSystem.Void);

            var vModule = new VariableDefinition("module", moduleType);
            getBindings.Body.Variables.Add(vModule);
            getBindings.Body.InitLocals = true;

            getBindings.Parameters.Add(new ParameterDefinition(References.DictionaryOfStringToBinding));

            var il = getBindings.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, References.RuntimeModule_Module);
            il.Emit(OpCodes.Castclass, moduleType);
            il.Emit(OpCodes.Stloc, vModule);

            foreach (var binding in ProviderGenerators) {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, binding.Key);
                il.Emit(OpCodes.Ldloc, vModule);
                il.Emit(OpCodes.Newobj, binding.GeneratedCtor);
                il.Emit(OpCodes.Callvirt, References.DictionaryOfStringToBinding_Add);
            }

            il.Emit(OpCodes.Ret);

            runtimeModule.Methods.Add(getBindings);
        }

        private void EmitCtor(TypeDefinition runtimeModule)
        {
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                ModuleDefinition.TypeSystem.Void);

            var il = ctor.Body.GetILProcessor();
            var vEntryPoints = new VariableDefinition("entryPoints", ModuleDefinition.Import(typeof (string[])));
            var vIncludes = new VariableDefinition("includes", ModuleDefinition.Import(typeof (Type[])));
            il.Body.InitLocals = true;
            il.Body.Variables.Add(vEntryPoints);
            il.Body.Variables.Add(vIncludes);
            
            // make array of entry point keys
            il.Emit(OpCodes.Ldc_I4, EntryPoints.Count);
            il.Emit(OpCodes.Newarr, ModuleDefinition.TypeSystem.String);
            il.Emit(OpCodes.Stloc, vEntryPoints);
            for (var i = 0; i < EntryPoints.Count; ++i) {
                il.Emit(OpCodes.Ldloc, vEntryPoints);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldstr, CompilerKeys.GetMemberKey(EntryPoints[i]));
                il.Emit(OpCodes.Stelem_Ref);
            }

            // make array of included module types
            il.Emit(OpCodes.Ldc_I4, IncludedModules.Count);
            il.Emit(OpCodes.Newarr, ModuleDefinition.Import(typeof(Type)));
            il.Emit(OpCodes.Stloc, vIncludes);
            for (var i = 0; i < IncludedModules.Count; ++i) {
                il.Emit(OpCodes.Ldloc, vIncludes);
                il.Emit(OpCodes.Ldc_I4, i);
                il.EmitType(IncludedModules[i]);
                il.Emit(OpCodes.Stelem_Ref);
            }

            // Push args (this, moduleType, entryPoints, includes, complete) and call base ctor
            il.Emit(OpCodes.Ldarg_0);
            il.EmitType(moduleType);
            il.Emit(OpCodes.Ldloc, vEntryPoints);
            il.Emit(OpCodes.Ldloc, vIncludes);
            il.EmitBoolean(IsComplete);
            il.Emit(OpCodes.Call, References.RuntimeModule_Ctor);

            il.Emit(OpCodes.Ret);

            runtimeModule.Methods.Add(ctor);
            generatedCtor = ctor;
        }
    }
}
