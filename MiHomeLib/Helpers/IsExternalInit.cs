#if NETSTANDARD2_0 || NETFRAMEWORK
// Older target frameworks need this compiler marker for record initialization.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
#endif
