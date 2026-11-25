using System;

namespace MiHomeLib.MiioDevices.Internal;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class MiotPropertyAttribute(string name, int siid, int piid, Type valueType) : Attribute
{
    public string Name { get; } = name;
    public int Siid { get; } = siid;
    public int Piid { get; } = piid;
    public Type ValueType { get; } = valueType;
    public bool Writable { get; set; } = true;
    /// <summary>For enums: the numeric type the device sends, e.g. typeof(byte)</summary>
    public Type WireType { get; set; }
    /// <summary>Text of the summary of the sync getter</summary>
    public string Summary { get; set; }
}
