using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>WXCJKG11LM Opple wireless switch with 2 buttons, supported by Lumi MQTT gateways.</summary>
[Obsolete("Use AqaraOppleTwoButtonsWirelessSwitch. AqaraOppleTwoButtonsWirelesSwitch will be removed in the next major version")]
public class AqaraOppleTwoButtonsWirelesSwitch : AqaraOppleWirelesSwitch
{
    public const string MARKET_MODEL = "WXCJKG11LM";
    public const string LUMI_MQTT_MODEL = "lumi.remote.b286opcn01";
    private protected override string MarketModel => MARKET_MODEL;
    private protected override string LumiMqttModel => LUMI_MQTT_MODEL;

    /// <summary>Raised for each supported click report from button 1.</summary>
    public event Func<ClickArg, Task> OnButton1ClickAsync = _ => Task.CompletedTask;
    /// <summary>Raised for each supported click report from button 2.</summary>
    public event Func<ClickArg, Task> OnButton2ClickAsync = _ => Task.CompletedTask;

    public AqaraOppleTwoButtonsWirelesSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : base(deviceId, shortId, loggerFactory) { }

    private protected override void ConfigureButtons(ProtocolDeviceMap map)
    {
        map.LumiMqtt("13.1.85", value => RaiseClick(value.ReadInt32(), OnButton1ClickAsync));
        map.LumiMqtt("13.2.85", value => RaiseClick(value.ReadInt32(), OnButton2ClickAsync));
    }
}
