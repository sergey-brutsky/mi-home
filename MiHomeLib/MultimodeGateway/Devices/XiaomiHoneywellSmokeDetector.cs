using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway.Devices;
public class XiaomiHoneywellSmokeDetector : ZigBeeManageableDevice
{
    public const string MARKET_MODEL = "JTYJ-GD-01LM/BW";
    public const string MODEL = "lumi.sensor_smoke";
    public enum SensivityMode 
    {
        // I don't know why exactly these numbers have been selected by vendor
        NoSmoke = 67174400,
        LowSmoke = 67239936,
        MiddleSmoke = 67305472,
    }    
    private const string SMOKE_DETECTED_RES_NAME = "13.1.85";
    private const string SMOKE_MODE_RES_NAME = "14.1.85";
    private const string SMOKE_DENSITY_RES_NAME = "0.1.85";
    public const int SELF_TEST_MAGIC_NUMBER = 50397184; // only God knows why this number
    public bool SmokeDetected { get; set; }
    public byte SmokeDensity { get; set; }
    public event Func<Task> OnSmokeDetectedAsync = () => Task.CompletedTask;
    public event Func<byte, Task> OnSmokeDensityChangedAsync = (_) => Task.CompletedTask;
    public event Func<SensivityMode, Task> OnSmokeSensivityModeChanged = (_) => Task.CompletedTask;
    public XiaomiHoneywellSmokeDetector(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : base(did, mqttTransport, loggerFactory)
    {
        Actions.Add(SMOKE_DETECTED_RES_NAME, async x =>
        {
            SmokeDetected = x.GetInt32() == 1;
            await OnSmokeDetectedAsync();
        });

        Actions.Add(SMOKE_DENSITY_RES_NAME, async x =>
        {
            var oldValue = SmokeDensity;
            SmokeDensity = (byte)x.GetInt32();
            await OnSmokeDensityChangedAsync(oldValue);
        });

        Actions.Add(SMOKE_MODE_RES_NAME, async x =>
        {
            await OnSmokeSensivityModeChanged((SensivityMode)x.GetInt32());
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["density", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        SmokeDensity = (byte)props[0].GetValue<int>();
        base.SetProps(props.Skip(1).ToArray());
    }
    public void SetSensivity(SensivityMode mode) => SendWriteCommand(SMOKE_MODE_RES_NAME, (int)mode);
    public void RunSelfTest() => SendWriteCommand(SMOKE_MODE_RES_NAME, SELF_TEST_MAGIC_NUMBER);
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Smoke detected: {SmokeDetected}, " +
            $"Smoke density: {SmokeDensity}%, " + base.ToString();
    }
}
