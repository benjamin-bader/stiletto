using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Abra.Fody
{
    public interface IWeaver
    {
        void EnqueueProviderBinding(string providerKey, TypeReference providedType);
        void EnqueueLazyBinding(string lazyKey, TypeReference lazyType);
        void LogWarning(string message);
        void LogError(string message);
    }
}
