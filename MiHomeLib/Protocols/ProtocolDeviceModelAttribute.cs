using System;

namespace MiHomeLib.Protocols;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class ProtocolDeviceModelAttribute(DeviceProtocol protocol, string model, int pdid = 0) : Attribute
{
    public DeviceProtocol Protocol { get; } = protocol;
    public string Model { get; } = model;
    public int Pdid { get; } = pdid;
}
