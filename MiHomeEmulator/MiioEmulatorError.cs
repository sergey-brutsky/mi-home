namespace MiHomeEmulator;

public sealed class MiioEmulatorError(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
