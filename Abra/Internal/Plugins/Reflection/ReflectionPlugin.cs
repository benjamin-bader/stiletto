using System;

namespace Abra.Internal.Plugins.Reflection
{
    internal sealed class ReflectionPlugin : IPlugin
    {
        public Binding GetInjectBinding(string key, string className, bool mustBeInjectable)
        {
            var t = TypeForClassname(className);
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

        private Type TypeForClassname(string className)
        {
            try {
                return Type.GetType(className, true);
            }
            catch (TypeLoadException ex) {
                throw new ArgumentException("Failed to load the type '" + className + "'.", ex);
            }
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
