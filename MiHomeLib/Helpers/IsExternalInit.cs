#if NETSTANDARD2_0 || NET481
// Older target frameworks need this compiler marker for record initialization.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
#endif
