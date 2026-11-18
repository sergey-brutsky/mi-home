using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using MiHomeUnitTests.Infrastructure;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class MiioHardeningTests : MiioDeviceTestBase
{
    public MiioHardeningTests() => SetupMiioInfo();

    [Fact]
    public void Repeated_Discovery_Of_The_Same_Device_Does_Not_Make_Id_Ambiguous()
    {
        var report = ("127.0.0.1", "0404", "a850", "token");
        MiioDevice.GetDeviceIdByIp("127.0.0.1", [report, report]).Should().Be("67414096");
    }

    [Fact]
    public void Conflicting_Device_Identities_For_One_Ip_Report_The_Address()
    {
        Action find = () => MiioDevice.GetDeviceIdByIp("127.0.0.1",
            [("127.0.0.1", "0404", "a850", "token"), ("127.0.0.1", "05c5", "a851", "other")]);
        find.Should().ThrowExactly<InvalidOperationException>().WithMessage("*127.0.0.1*conflicting*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000000")]
    [InlineData("zz000000000000000000000000000000")]
    public void Invalid_Token_Is_Rejected_By_The_Public_Constructor(string token)
    {
        Action create = () => { using var device = new XiaomiAirHumidifierV1("127.0.0.1", token); };
        create.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("token");
    }

    [Theory]
    [InlineData("ap", "\"not-json\"")]
    [InlineData("netif", "\"not-json\"")]
    [InlineData("ap", "null")]
    [InlineData("netif", "[]")]
    [InlineData("life", "-1")]
    [InlineData("life", "\"bad\"")]
    [InlineData("miio_ver", "17")]
    [InlineData("mac", "{}")]
    [InlineData("fw_ver", "17")]
    [InlineData("hw_ver", "false")]
    public async Task Invalid_Info_Field_Is_Contextual_And_Disposes_Transport(string fieldName, string raw)
    {
        var response = JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        response["result"][fieldName] = JsonNode.Parse(raw);
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        Action create = () => new MiotGenericDevice("test", _miioTransport.Object, 0);
        create.Should().ThrowExactly<InvalidOperationException>().WithMessage($"*miIO.info*{fieldName}*");
        _miioTransport.Verify(t => t.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("life")]
    [InlineData("ap.rssi")]
    [InlineData("ap.ssid")]
    [InlineData("netif.localIp")]
    public async Task Missing_Info_Field_Identifies_The_Path(string path)
    {
        var response = JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        var parts = path.Split('.');
        var parent = parts.Length == 1 ? response["result"] : response["result"][parts[0]];
        parent.AsObject().Remove(parts.Last());
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        Action create = () => new MiotGenericDevice("test", _miioTransport.Object, 0);
        create.Should().ThrowExactly<InvalidOperationException>().WithMessage($"*miIO.info*{path}*");
        _miioTransport.Verify(t => t.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1;2")]
    [InlineData("1;2;0;3;4")]
    [InlineData("bad;2;0;3")]
    [InlineData("1;70000;0;3")]
    [InlineData("1;2;bad;3")]
    [InlineData("1;2;0;256")]
    public async Task Malformed_Cycle_Data_Is_Contextual_In_Both_Apis(string raw)
    {
        using var plug = new XiaomiSmartPlug2Euro(_miioTransport.Object);
        SetupGetProperties((5, 2, raw));
        Func<Task> read = () => plug.GetCycleDataAsync();
        await read.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("*cycle*5/2*");
        Action readSync = () => _ = plug.GetCycleData();
        readSync.Should().ThrowExactly<InvalidOperationException>().WithMessage("*cycle*5/2*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,22,0,8,0")]
    [InlineData("[1,22]")]
    [InlineData("[1,22,0,8,0,7]")]
    [InlineData("[1,bad,0,8,0]")]
    [InlineData("[1,40000,0,8,0]")]
    public async Task Malformed_Dnd_Data_Is_Contextual_In_Both_Apis(string raw)
    {
        using var vacuum = new XiaomiRobotVacuumMop3C(_miioTransport.Object);
        SetupGetProperties((12, 7, raw));
        Func<Task> read = () => vacuum.GetDndSettingsAsync();
        await read.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("*DND*12/7*");
        Action readSync = () => vacuum.GetDndSettings();
        readSync.Should().ThrowExactly<InvalidOperationException>().WithMessage("*DND*12/7*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Malformed_Consumable_Number_Preserves_The_Original_Exception(int index)
    {
        using var vacuum = new XiaomiRobotVacuumMop3C(_miioTransport.Object);
        SetupGetProperties(Enumerable.Range(0, 8)
            .Select(i => (7, 8 + i, (object)(i == index ? "bad" : "10"))).ToArray());
        Func<Task> read = () => vacuum.GetConsumablesAsync();
        var error = await read.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("*consumables*7/8-15*");
        error.Which.InnerException.Should().BeOfType<FormatException>();
    }

    [Theory]
    [InlineData("rssi")]
    [InlineData("freq")]
    public async Task Invalid_Wifi_Number_Reports_The_Path_And_Preserves_The_Cause(string fieldName)
    {
        var response = JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        response["result"]["ap"][fieldName] = "bad";
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        Action create = () => new MiotGenericDevice("test", _miioTransport.Object, 0);
        var error = create.Should().ThrowExactly<InvalidOperationException>().WithMessage($"*miIO.info*ap.{fieldName}*");
        error.Which.InnerException.Should().BeOfType<FormatException>();
        _miioTransport.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Missing_Optional_Wifi_Frequency_Still_Defaults_To_Zero()
    {
        var response = JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        response["result"]["ap"].AsObject().Remove("freq");
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        using var device = new MiotGenericDevice("test", _miioTransport.Object, 0);
        device.Wifi.Freq.Should().Be(0);
    }

    [Fact]
    public async Task Malformed_Encoded_Info_Object_Preserves_Json_Exception()
    {
        var response = JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        response["result"]["ap"] = "not-json";
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        Action create = () => new MiotGenericDevice("test", _miioTransport.Object, 0);
        var error = create.Should().ThrowExactly<InvalidOperationException>().WithMessage("*miIO.info ap*");
        error.Which.InnerException.Should().BeAssignableTo<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Null_Info_Response_Reports_The_Operation_And_Disposes_Transport()
    {
        _miioTransport.Setup(t => t.SendMessageAsync(It.IsAny<string>())).ReturnsAsync((string)null);
        Action create = () => new MiotGenericDevice("test", _miioTransport.Object, 0);
        create.Should().ThrowExactly<InvalidOperationException>().WithMessage("*miIO.info*null*");
        _miioTransport.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public void Changing_Checksum_Without_Changing_Identity_Does_Not_Make_Id_Ambiguous()
    {
        MiioDevice.GetDeviceIdByIp("127.0.0.1",
            [("127.0.0.1", "0404", "a850", "first"), ("127.0.0.1", "0404", "a850", "second")])
            .Should().Be("67414096");
    }
}
