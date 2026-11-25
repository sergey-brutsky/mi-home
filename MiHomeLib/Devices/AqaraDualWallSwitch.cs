using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>QBKG03LM wired wall switch with two buttons, no neutral, shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraDualWallSwitch : ProtocolMappedDeviceBase, IProtocolMappedManageableDevice, IProtocolMappedGateway2ManageableDevice
{
    public const string MARKET_MODEL = "QBKG03LM";
    public const string XIAOMI_GATEWAY2_MODEL = "ctrl_neutral2";
    public const string LUMI_MQTT_MODEL = "lumi.ctrl_neutral2";

    public enum Button
    {
        Left,
        Right,
        Both,
    }

    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        LongPressHold = 16,
        LongPressRelease = 17,
    }

    private const string LEFT_CHANNEL_RES_NAME = "4.1.85";
    private const string RIGHT_CHANNEL_RES_NAME = "4.2.85";
    private const string LEFT_WIRELESS_RES_NAME = "4.10.85";
    private const string RIGHT_WIRELESS_RES_NAME = "4.11.85";
    private const string LED_RES_NAME = "8.0.2032";

    private ZigBeeTransport _zigBeeTransport;
    private IGateway2Writer _gateway2;

    public override string DeviceId { get; }
    public int ShortId { get; }

    /// <summary>
    /// State of the left channel ("on"/"off"), "idle" until the first report
    /// </summary>
    public string StatusLeft { get; private set; } = "idle";

    /// <summary>
    /// State of the right channel ("on"/"off"), "idle" until the first report
    /// </summary>
    public string StatusRight { get; private set; } = "idle";

    /// <summary>
    /// Raised on every report of the left channel, the new state is passed as an argument
    /// </summary>
    public event Func<string, Task> OnSwitchChannelLeftAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the right channel, the new state is passed as an argument
    /// </summary>
    public event Func<string, Task> OnSwitchChannelRightAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised when a button is pressed, reported by Lumi MQTT gateways only.
    /// Useful in wireless mode, when the button does not switch its channel
    /// </summary>
    public event Func<Button, ClickArg, Task> OnButtonClickAsync = (_, _) => Task.CompletedTask;

    public AqaraDualWallSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) => _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    void IProtocolMappedGateway2ManageableDevice.SetGateway2Writer(IGateway2Writer writer) => _gateway2 = writer;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("channel_0", value => UpdateLeft(value.ReadString()));
        map.XiaomiGateway2("channel_1", value => UpdateRight(value.ReadString()));

        map.LumiMqtt(LEFT_CHANNEL_RES_NAME, "neutral_0",
            value => UpdateLeft(value.ReadInt32() == 1 ? "on" : "off"),
            value => StatusLeft = InitialChannelState(value));
        map.LumiMqtt(RIGHT_CHANNEL_RES_NAME, "neutral_1",
            value => UpdateRight(value.ReadInt32() == 1 ? "on" : "off"),
            value => StatusRight = InitialChannelState(value));
        map.LumiMqtt("13.1.85", value => RaiseClick(Button.Left, value));
        map.LumiMqtt("13.2.85", value => RaiseClick(Button.Right, value));
        map.LumiMqtt("13.5.85", value => RaiseClick(Button.Both, value));
        // Settings are confirmed by reports after they are written, nothing to update
        map.LumiMqtt(LEFT_WIRELESS_RES_NAME, _ => { });
        map.LumiMqtt(RIGHT_WIRELESS_RES_NAME, _ => { });
        map.LumiMqtt(LED_RES_NAME, _ => { });
    }

    public void ChannelLeftPowerOn() => RunSync(() => ChannelLeftPowerOnAsync());

    /// <inheritdoc cref="ChannelLeftPowerOn"/>
    public Task ChannelLeftPowerOnAsync() => SetChannelAsync(Button.Left, true);
    public void ChannelLeftPowerOff() => RunSync(() => ChannelLeftPowerOffAsync());

    /// <inheritdoc cref="ChannelLeftPowerOff"/>
    public Task ChannelLeftPowerOffAsync() => SetChannelAsync(Button.Left, false);
    public void ChannelRightPowerOn() => RunSync(() => ChannelRightPowerOnAsync());

    /// <inheritdoc cref="ChannelRightPowerOn"/>
    public Task ChannelRightPowerOnAsync() => SetChannelAsync(Button.Right, true);
    public void ChannelRightPowerOff() => RunSync(() => ChannelRightPowerOffAsync());

    /// <inheritdoc cref="ChannelRightPowerOff"/>
    public Task ChannelRightPowerOffAsync() => SetChannelAsync(Button.Right, false);

    /// <summary>
    /// In wireless mode the button only raises <see cref="OnButtonClickAsync"/> and does not switch its channel.
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetWirelessMode(Button button, bool wireless) => RunSync(() => SetWirelessModeAsync(button, wireless));

    /// <inheritdoc cref="SetWirelessMode"/>
    public Task SetWirelessModeAsync(Button button, bool wireless)
    {
        try
        {
            var resource = button switch
            {
                Button.Left => LEFT_WIRELESS_RES_NAME,
                Button.Right => RIGHT_WIRELESS_RES_NAME,
                _ => throw new ArgumentException("Wireless mode is set for the left or the right button", nameof(button)),
            };

            SendMqttWriteCommand(resource, wireless ? 1 : 0);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetLed(bool enabled) => RunSync(() => SetLedAsync(enabled));

    /// <inheritdoc cref="SetLed"/>
    public Task SetLedAsync(bool enabled)
    {
        try
        {
            SendMqttWriteCommand(LED_RES_NAME, enabled ? 1 : 0);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    private async Task SetChannelAsync(Button button, bool on)
    {
        if (_gateway2 is not null)
        {
            var channel = button == Button.Left ? "channel_0" : "channel_1";
            await _gateway2.WriteAsync(DeviceId, XIAOMI_GATEWAY2_MODEL, (channel, on ? "on" : "off")).ConfigureAwait(false);
            return;
        }

        SendMqttWriteCommand(button == Button.Left ? LEFT_CHANNEL_RES_NAME : RIGHT_CHANNEL_RES_NAME, on ? 1 : 0);
    }

    private void SendMqttWriteCommand(string resource, int value)
    {
        if (_zigBeeTransport is null)
            throw new NotSupportedException(_gateway2 is null
                ? $"Device '{DeviceId}' has not been connected to a gateway, it cannot accept commands."
                : $"This command of device '{DeviceId}' is supported only on Lumi MQTT gateways (Multimode Gateway, Aqara Hub E1).");

        _zigBeeTransport.SendWriteCommand(DeviceId, resource, value);
    }

    // The gateway may answer with "on"/"off" or with 1/0
    private static string InitialChannelState(JsonNode value) =>
        value is JsonValue v && v.TryGetValue<string>(out var text) ? text
        : value is JsonValue n && n.TryGetValue<int>(out var number) ? (number == 1 ? "on" : "off")
        : "idle";

    private void RaiseClick(Button button, JsonElement value)
    {
        var code = value.ReadInt32();
        if (!Enum.IsDefined(typeof(ClickArg), code))
        {
            _logger.LogWarning("Click code '{Code}' of button {Button} is not supported yet. Please contribute to support.", code, button);
            return;
        }

        RaiseEvent(nameof(OnButtonClickAsync), OnButtonClickAsync, button, (ClickArg)code);
    }

    private void UpdateLeft(string status)
    {
        StatusLeft = status;
        RaiseEvent(nameof(OnSwitchChannelLeftAsync), OnSwitchChannelLeftAsync, status);
    }

    private void UpdateRight(string status)
    {
        StatusRight = status;
        RaiseEvent(nameof(OnSwitchChannelRightAsync), OnSwitchChannelRightAsync, status);
    }

    public override string ToString()
    {
        var model = Model ?? XIAOMI_GATEWAY2_MODEL;
        return $"Device: {MARKET_MODEL} {model} {DeviceId}, Left: {StatusLeft}, Right: {StatusRight}";
    }
}
