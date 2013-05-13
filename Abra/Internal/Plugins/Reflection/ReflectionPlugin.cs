using System;
using System.Diagnostics;

namespace Abra.Internal.Plugins.Reflection
{
    internal sealed class ReflectionPlugin : IPlugin
    {
        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            FailIfNoReflection();
            var t = ReflectionUtils.GetType(className);
            if (t.IsInterface)
            {
                return null;
            }

            return ReflectionInjectBinding.Create(t, mustBeInjectable);
        }

        public Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey)
        {
            FailIfNoReflection();
            return new ReflectionLazyBinding(key, requiredBy, lazyKey);
        }

        public Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey)
        {
            FailIfNoReflection();
            return new ReflectionProviderBinding(key, requiredBy, mustBeInjectable, providerKey);
        }

        public RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance)
        {
            FailIfNoReflection();
            var attr = moduleType.GetSingleAttribute<ModuleAttribute>();

            if (moduleType.BaseType != typeof(object))
            {
                throw new ArgumentException("Modules must inherit only from System.Object.");
            }

            return new ReflectionRuntimeModule(moduleType, attr);
        }

        [Conditional("NO_REFLECTION_PLUGIN")]
        private static void FailIfNoReflection()
        {
            throw new NotSupportedException("Reflection bindings have been disabled.");
        }
    }
}
