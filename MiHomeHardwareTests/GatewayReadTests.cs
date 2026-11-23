using FluentAssertions;
using Xunit;

namespace MiHomeHardwareTests;

[Collection("Hardware")]
public sealed class GatewayReadTests(HardwareFixture fixture)
{
    [SkippableFact]
    public async Task Gateway2_Discovery_And_Read_Commands_Work()
    {
        Skip.If(fixture.Gateway2 is null, "MIHOME_GW2_IP/MIHOME_GW2_TOKEN are not set");
        var gateway = fixture.Gateway2!;
        gateway.Sid.Should().NotBeNullOrEmpty();
        gateway.ProtoVersion.Should().NotBeNullOrEmpty();
        gateway.GetDevices().Should().NotBeEmpty();
        (await gateway.GetArmingVolumeAsync()).Should().BeInRange(0, 100);
        await gateway.IsArmingOnAsync();
    }

    [SkippableFact]
    public void Mqtt_Discovery_And_Device_Information_Work()
    {
        Skip.If(fixture.Mqtt is null, "MQTT gateway configuration is not set");
        var gateway = fixture.Mqtt!;
        gateway.GetDevices().Should().NotBeEmpty();
        gateway.UptimeSeconds.Should().BeGreaterThan(0);
        gateway.FirmwareVersion.Should().NotBeNullOrEmpty();
    }
}
