using System;
using System.Collections.Generic;
using System.Reflection;

namespace Abra.Internal.Plugins.Reflection
{
    internal class ReflectionRuntimeModule : RuntimeModule
    {
        private const BindingFlags DeclaredMethods =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        public ReflectionRuntimeModule(Type moduleType, ModuleAttribute attribute)
            : base(moduleType,
                   Array.ConvertAll(attribute.EntryPoints, Key.GetMemberKey),
                   attribute.IncludedModules,
                   attribute.IsComplete)
        {
        }

        public override void GetBindings(IDictionary<string, Binding> bindings)
        {
            for (var t = ModuleType; t != typeof(object); t = t.BaseType)
            {
                // t will always be typeof(object) before it is null.
// ReSharper disable PossibleNullReferenceException
                var methods = t.GetMethods(DeclaredMethods);
// ReSharper restore PossibleNullReferenceException
                for (var i = 0; i < methods.Length; ++i)
                {
                    var m = methods[i];
                    if (!m.HasAttribute<ProvidesAttribute>())
                    {
                        continue;
                    }

                    var key = Key.Get(m.ReturnType, m.GetQualifierName());
                    AddNewBinding(bindings, key, m);

                }
            }
        }

        public override object CreateModule()
        {
            if (ModuleType.BaseType != typeof(object))
            {
                throw new NotSupportedException("Modules must inherit only from System.Object.");
            }

            var ctor = ModuleType.GetConstructor(Type.EmptyTypes);

            if (ctor == null)
            {
                throw new NotSupportedException("Modules must have a public default constructor.");
            }

            return ctor.Invoke(null);
        }

        private void AddNewBinding(IDictionary<string, Binding> bindings, string key, MethodInfo method)
        {
            bindings[key] = new ProviderMethodBinding(method, key, Module);
        }

        private class ProviderMethodBinding : Binding
        {
            private readonly MethodInfo method;
            private readonly object target;
            private Binding[] methodParameterBindings;

            public ProviderMethodBinding(MethodInfo method, string providerKey, object target)
                : base(providerKey, null, method.HasAttribute<SingletonAttribute>(), method)
            {
                this.method = method;
                this.target = target;
            }

            public override void Resolve(Resolver resolver)
            {
                var parameters = method.GetParameters();

                methodParameterBindings = new Binding[parameters.Length];
                for (var i = 0; i < parameters.Length; ++i)
                {
                    var key = Key.Get(parameters[i].ParameterType, parameters[i].GetQualifierName());
                    methodParameterBindings[i] = resolver.RequestBinding(key, method);
                }
            }

            public override object Get()
            {
                var args = new object[methodParameterBindings.Length];
                for (var i = 0; i < methodParameterBindings.Length; ++i)
                {
                    args[i] = methodParameterBindings[i].Get();
                }

                return method.Invoke(target, args);
            }

            public override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
            {
                injectDependencies.UnionWith(methodParameterBindings);
            }

            public override void InjectProperties(object instance)
            {
                throw new NotSupportedException("Provider methods can't inject members.");
            }
        }
    }
}
