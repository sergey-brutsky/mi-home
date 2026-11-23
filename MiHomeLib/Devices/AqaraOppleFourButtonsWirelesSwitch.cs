using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>WXCJKG12LM Opple wireless switch with 4 buttons, supported by Lumi MQTT gateways.</summary>
[Obsolete("Use AqaraOppleFourButtonsWirelessSwitch. AqaraOppleFourButtonsWirelesSwitch will be removed in the next major version")]
public class AqaraOppleFourButtonsWirelesSwitch : AqaraOppleWirelesSwitch
{
    public const string MARKET_MODEL = "WXCJKG12LM";
    public const string LUMI_MQTT_MODEL = "lumi.remote.b486opcn01";
    private protected override string MarketModel => MARKET_MODEL;
    private protected override string LumiMqttModel => LUMI_MQTT_MODEL;

    /// <summary>Raised for each supported click report from button 1.</summary>
    public event Func<ClickArg, Task> OnButton1ClickAsync = _ => Task.CompletedTask;
    /// <summary>Raised for each supported click report from button 2.</summary>
    public event Func<ClickArg, Task> OnButton2ClickAsync = _ => Task.CompletedTask;
    /// <summary>Raised for each supported click report from button 3.</summary>
    public event Func<ClickArg, Task> OnButton3ClickAsync = _ => Task.CompletedTask;
    /// <summary>Raised for each supported click report from button 4.</summary>
    public event Func<ClickArg, Task> OnButton4ClickAsync = _ => Task.CompletedTask;

    public AqaraOppleFourButtonsWirelesSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : base(deviceId, shortId, loggerFactory) { }

    private protected override void ConfigureButtons(ProtocolDeviceMap map)
    {
        map.LumiMqtt("13.1.85", value => RaiseClick(value.ReadInt32(), OnButton1ClickAsync));
        map.LumiMqtt("13.2.85", value => RaiseClick(value.ReadInt32(), OnButton2ClickAsync));
        map.LumiMqtt("13.3.85", value => RaiseClick(value.ReadInt32(), OnButton3ClickAsync));
        map.LumiMqtt("13.4.85", value => RaiseClick(value.ReadInt32(), OnButton4ClickAsync));
    }
}
