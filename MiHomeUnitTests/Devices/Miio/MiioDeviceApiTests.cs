using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Xunit;

namespace MiHomeUnitTests.Devices.Miio;

public class MiioDeviceApiTests
{
    public static IEnumerable<object[]> Devices =>
    [
        [typeof(MijiaSmartSocket2China)],
        [typeof(XiaomiSmartPlug2Euro)],
        [typeof(XiaomiSmartAirPurifier4Lite)],
        [typeof(XiaomiAirHumidifierV1)],
        [typeof(XiaomiRobotVacuumCleanerV1)],
        [typeof(XiaomiRobotVacuumMop3C)],
        [typeof(XiaomiRobotVacuumX20Plus)]
    ];

    [Theory]
    [MemberData(nameof(Devices))]
    public void Device_Operations_Are_Exposed_As_Methods(Type deviceType)
    {
        // Metadata inherited from MiotGenericDevice and properties of returned snapshots are local data.
        deviceType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void Each_Synchronous_Operation_Has_A_Matching_Async_Method(Type deviceType)
    {
        var methods = deviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name != nameof(ToString) && !method.Name.EndsWith("Async", StringComparison.Ordinal));

        foreach (var method in methods)
        {
            var parameters = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var asyncMethod = deviceType.GetMethod(method.Name + "Async", parameters);
            asyncMethod.Should().NotBeNull($"{deviceType.Name}.{method.Name} needs an async counterpart");
            var expectedReturn = method.ReturnType == typeof(void)
                ? typeof(Task)
                : typeof(Task<>).MakeGenericType(method.ReturnType);
            asyncMethod.ReturnType.Should().Be(expectedReturn);
        }
    }
}
