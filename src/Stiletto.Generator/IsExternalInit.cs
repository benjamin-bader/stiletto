// Polyfill: records / init-only setters need this type, which is absent from netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
