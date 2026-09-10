using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MiHomeLib;

/// <summary>
/// Scans the executing assembly for device types that inherit from specified base types
/// and extracts their MODEL static field values via reflection.
/// Used by both MqttGatewayBase and XiaomiGateway2 to discover supported device types.
/// </summary>
internal static class DeviceTypeScanner
{
    private static readonly BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;

    /// <summary>
    /// Find all non-abstract device types inheriting from TBase and their MODEL values
    /// </summary>
    public static IEnumerable<(Type type, string model)> FindDeviceTypes<TBase>() where TBase : class
    {
        return FindDeviceTypes(typeof(TBase));
    }

    /// <summary>
    /// Find all non-abstract device types inheriting from any of the specified base types and their MODEL values
    /// </summary>
    public static IEnumerable<(Type type, string model)> FindDeviceTypes(params Type[] baseTypes)
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && baseTypes.Any(bt => x.IsSubclassOf(bt)))
            .Select(type => (type, model: type.GetField("MODEL", StaticPublic).GetValue(type).ToString()));
    }

    /// <summary>
    /// Read a static field value from a device type
    /// </summary>
    public static T GetStaticField<T>(Type type, string fieldName)
    {
        return (T)type.GetField(fieldName, StaticPublic).GetValue(type);
    }
}