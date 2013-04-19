using System;

namespace Abra.Internal.Plugins.Reflection
{
    internal sealed class ReflectionPlugin : IPlugin
    {
        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            var t = ReflectionUtils.GetType(className);
            if (t.IsInterface)
            {
                return null;
            }

            return ReflectionInjectBinding.Create(t, mustBeInjectable);
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            return new ReflectionLazyBinding(key, requiredBy, lazyKey);
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            return new ReflectionProviderBinding(key, requiredBy, mustBeInjectable, providerKey);
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            var attr = moduleType.GetSingleAttribute<ModuleAttribute>();

            if (moduleType.BaseType != typeof(object))
            {
                throw new ArgumentException("Modules must inherit only from System.Object.");
            }

            return new ReflectionRuntimeModule(moduleType, attr);
        }
    }
}
