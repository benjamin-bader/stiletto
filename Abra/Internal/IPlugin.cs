using System;

namespace Abra.Internal
{
    public interface IPlugin
    {
        Binding GetInjectBinding(string key, string className, bool mustBeInjectable);
        Binding GetLazyInjectBinding(string key, object requiredBy, string lazyKey);
        Binding GetIProviderInjectBinding(string key, object requiredBy, bool mustBeInjectable, string providerKey);
        RuntimeModule GetRuntimeModule(Type moduleType, object moduleInstance);
    }
}
