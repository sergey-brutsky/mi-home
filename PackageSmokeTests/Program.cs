using System;
using System.Threading.Tasks;
using MiHomeLib.MiioDevices;

internal static class Program
{
    private static async Task Main()
    {
        using var device = new ProbeDevice(new ProbeTransport());
        if (await device.GetStateAsync() != "on")
            throw new InvalidOperationException("Unexpected property response from the packaged library.");
        var framework = ((System.Runtime.Versioning.TargetFrameworkAttribute)Attribute.GetCustomAttribute(
            typeof(MiioDevice).Assembly, typeof(System.Runtime.Versioning.TargetFrameworkAttribute))).FrameworkName;
#if NET462
        const string expected = ".NETFramework,Version=v4.6.2";
#elif NET8_0
        const string expected = ".NETStandard,Version=v2.0";
#else
        const string expected = ".NETCoreApp,Version=v10.0";
#endif
        if (framework != expected) throw new InvalidOperationException("Unexpected package asset " + framework);
        Console.WriteLine("Package command succeeded using " + framework);
    }

    private sealed class ProbeDevice(IMiioTransport transport) : MiioDevice(transport)
    {
        public async Task<string> GetStateAsync() => (await GetPropsAsync("power"))[0];
    }

    private sealed class ProbeTransport : IMiioTransport
    {
        public string Ip => "127.0.0.1";
        public string Token => "unused";
        public string SendMessage(string message) => "{\"result\":[\"on\"]}";
        public Task<string> SendMessageAsync(string message) => Task.FromResult(SendMessage(message));
        public string SendMessageRepeated(string message, int times = 3) => SendMessage(message);
        public void Dispose() { }
    }
}
