using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Abra.Fody
{
    public interface IWeaver
    {
        void EnqueueProviderBinding(TypeReference providedType);
        void EnqueueLazyBinding(TypeReference lazyType);
        void LogWarning(string message);
        void LogError(string message);
    }
}
