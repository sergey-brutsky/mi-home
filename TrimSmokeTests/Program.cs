using System.Text.Json;
using MiHomeLib.MiioDevices;
using MiHomeLib.Protocols;
using Microsoft.Extensions.Logging.Abstractions;

using var probe = new ProbeDevice(new ProbeTransport());
if (await probe.GetStateAsync() != "on") throw new InvalidOperationException("Property read failed.");
using var transport = new ProbeTransport();
using var vacuum = new XiaomiRobotVacuumX20Plus(transport);
await vacuum.StartRoomCleaningAsync([new(1)]);
await vacuum.SetExtendedSwitchAsync("test", 1);
if (transport.RoomCommands != 1 || transport.SettingCommands != 1)
    throw new InvalidOperationException("Source-generated X20 Plus commands were not observed.");
foreach (var entry in ProtocolDeviceCatalog.Entries)
{
    var metadata = entry.DeviceType.GetCustomAttributes(typeof(ProtocolDeviceModelAttribute), false)
        .Cast<ProtocolDeviceModelAttribute>();
    foreach (var model in metadata)
    {
        if (!ProtocolDeviceRegistry.TryCreate(model.Protocol, model.Model, "smoke-device", 0, NullLoggerFactory.Instance, out var device))
            throw new InvalidOperationException("Catalog model was trimmed: " + model.Model);
        using (device)
            if (!device.ToString()!.Contains("smoke-device")) throw new InvalidOperationException("Device identity was lost.");
    }
}
Console.WriteLine("PASS: device catalog, property reads and X20 Plus JSON commands with reflection serialization disabled.");

sealed class ProbeDevice(IMiioTransport transport) : MiioDevice(transport)
{
    public async Task<string> GetStateAsync() => (await GetPropsAsync("power"))[0];
}

sealed class ProbeTransport : IMiioTransport, IDisposable
{
    public string Ip => "127.0.0.1";
    public string Token => "unused";
    public int RoomCommands { get; private set; }
    public int SettingCommands { get; private set; }
    public string SendMessage(string message) => SendMessageAsync(message).GetAwaiter().GetResult();
    public string SendMessageRepeated(string message, int times) => SendMessage(message);
    public Task<string> SendMessageAsync(string message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;
        var method = root.GetProperty("method").GetString();
        var parameters = root.GetProperty("params");
        var response = method switch
        {
            "miIO.info" => "{\"result\":{\"life\":1,\"miio_ver\":\"test\",\"mac\":\"00:00:00:00:00:00\",\"fw_ver\":\"test\",\"hw_ver\":\"test\",\"ap\":{\"ssid\":\"test\",\"bssid\":\"test\",\"rssi\":-1},\"netif\":{\"localIp\":\"127.0.0.1\",\"mask\":\"255.0.0.0\",\"gw\":\"127.0.0.1\"}}}",
            "get_prop" => "{\"result\":[\"on\"]}",
            "action" => RoomResponse(parameters),
            "set_properties" => SettingResponse(parameters),
            _ => throw new InvalidOperationException("Unexpected command: " + method)
        };
        return Task.FromResult(response);
    }
    private string RoomResponse(JsonElement parameters)
    {
        var inputs = parameters.GetProperty("in");
        if (inputs[0].GetProperty("value").GetInt32() != 18) throw new InvalidOperationException("Wrong room work mode.");
        using var data = JsonDocument.Parse(inputs[1].GetProperty("value").GetString()!);
        if (data.RootElement.GetProperty("selects")[0][0].GetInt32() != 1) throw new InvalidOperationException("Wrong room ID.");
        RoomCommands++;
        return "{\"result\":{\"code\":0}}";
    }
    private string SettingResponse(JsonElement parameters)
    {
        using var data = JsonDocument.Parse(parameters[0].GetProperty("value").GetString()!);
        if (data.RootElement.GetProperty("k").GetString() != "test" || data.RootElement.GetProperty("v").GetInt32() != 1)
            throw new InvalidOperationException("Wrong extended-switch JSON.");
        SettingCommands++;
        return "{\"result\":[{\"code\":0}]}";
    }
    public void Dispose() { }
}
