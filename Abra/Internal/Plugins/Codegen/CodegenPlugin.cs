using System;

namespace Abra.Internal.Plugins.Codegen
{
    public class CodegenPlugin : IPlugin
    {
        public const string InjectSuffix    = "_CompiledBinding";
        public const string ModuleSuffix    = "_CompiledModule";
        public const string LazySuffix      = "_CompiledLazyBinding";
        public const string IProviderSuffix = "_CompiledProviderBinding";

        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            return GetObjectOfTypeName<Binding>(className, InjectSuffix);
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            return GetObjectOfTypeName<Binding>(
                Key.GetTypeName(lazyKey),
                LazySuffix,
                new[] { key, requiredBy, lazyKey });
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            return GetObjectOfTypeName<Binding>(
                Key.GetTypeName(providerKey),
                IProviderSuffix,
                new[] { key, requiredBy, mustBeInjectable, providerKey });
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {

            return GetObjectOfTypeName<RuntimeModule>(moduleType.FullName, ModuleSuffix);
        }

        private T GetObjectOfTypeName<T>(string typeName, string suffix, object[] ctorArgs = null)
        {
            typeName += suffix;
            typeName = typeName.Replace('+', '_');
            var t = ReflectionUtils.GetType(typeName);

            if (t == null) {
                throw new ArgumentException("Could not find generated class of type: " + typeName);
            }

            return (T) Activator.CreateInstance(t, ctorArgs);
        }
    }
}
