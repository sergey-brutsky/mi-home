using System.Threading.Tasks;

namespace MiHomeLib.Devices;

public interface ITemperatureSensor
{
    float Temperature { get; }
}

public interface IHumiditySensor
{
    float Humidity { get; }
}

public interface IPressureSensor
{
    float Pressure { get; }
}

/// <remarks>0 until reported; Xiaomi Gateway 2 does not report it.</remarks>
public interface IBatteryPowered
{
    byte BatteryPercent { get; }
}

public interface ILinkQualityReporter
{
    byte LinkQuality { get; }
}

public interface IPowerMeter
{
    float LoadPower { get; }
}

/// <summary>The value is null while the contact state is unknown.</summary>
public interface IContactSensor
{
    bool? IsOpen { get; }
}

/// <summary>The value is null while the smoke state is unknown.</summary>
public interface ISmokeSensor
{
    bool? IsSmokeDetected { get; }
}

public interface ISwitchable
{
    /// <summary>The value is null while the switch state is unknown.</summary>
    bool? IsOn { get; }

    Task TurnOnAsync();
    Task TurnOffAsync();
    Task ToggleAsync();
}
