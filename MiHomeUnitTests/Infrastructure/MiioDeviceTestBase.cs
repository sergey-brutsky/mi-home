using MiHomeLib.MiioDevices;
using MiHomeLib.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using MiHomeLib;
using Moq;

namespace MiHomeUnitTests.Infrastructure;

public abstract class MiioDeviceTestBase
{
    private Mock<IMiioTransport> _transportMock;
    protected Mock<IMiioTransport> _miioTransport
    {
        get => _transportMock ??= new Mock<IMiioTransport>();
        set => _transportMock = value;
    }
    protected void SetupMiioInfo()
    {
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => System.Text.Json.Nodes.JsonNode.Parse(s)["method"].GetValue<string>() == "miIO.info")))
            .ReturnsAsync(new
            {
                id = 1,
                result = new
                {
                    uptime = 502175,
                    life = 502175,
                    miio_ver = "0.0.9",
                    mac = "34:EF:44:49:BC:63",
                    fw_ver = "1.0.5_0008",
                    hw_ver = "Linux",
                    ap = new { ssid = "ssid1", bssid = "bssid1", rssi = -35, freq = 2437 },
                    netif = new { localIp = "192.168.1.100", mask = "255.255.255.0", gw = "192.168.1.1" },
                }
            }.ToJson());
    }
    protected void VerifyMethod(string method, params object[] args)
    {
        VerifyMethodWithId(method, 1, args);
    }
    protected void VerifyMethodWithId(string method, int id, params object[] args)
    {
        _miioTransport
            .Verify(x => x.SendMessageAsync(new { id, method, @params = args}.ToJson()), Times.Once());
    }
    protected static async Task Call(bool isAsync, Action sync, Func<Task> async)
    {
        if (isAsync) await async();
        else sync();
    }
    protected static async Task<T> Call<T>(bool isAsync, Func<T> sync, Func<Task<T>> async) => isAsync ? await async() : sync();
    protected void SendResultMethod(string method, params object[] args)
    {
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => method == string.Empty || System.Text.Json.Nodes.JsonNode.Parse(s)["method"].GetValue<string>() == method)))
            .ReturnsAsync(new { result = args, id = 1 }.ToJson());
    }
    protected void SetupGetProperties(params (int siid, int piid, object value)[] args)
    {
        SendResultMethod("get_properties", [.. args.Select(x => new { did = $"{x.siid}-{x.piid}", x.value, code = 0 })]);
    }
    protected void SetupGetProperties(params (int siid, int piid, string did, object value)[] args)
    {
        SendResultMethod("get_properties", [.. args.Select(x => new { x.did, x.value, code = 0 })]);
    }
    protected void SetupSetProperties()
    {
        SendResultMethod("set_properties", new { code = 0 });
    }
    protected void VerifyGetProperties(int siid, int piid, string did, int initialId = 2)
    {
        VerifyGetProperties(initialId, (siid, piid, did));
    }
    protected void VerifyGetProperties(int siid, int piid, int initialId = 2)
    {
        VerifyGetProperties(initialId, (siid, piid));
    }
    protected void VerifyGetProperties(int initialId, params (int siid, int piid, string did)[] args)
    {
        VerifyMethodWithId("get_properties", initialId, [.. args.Select(x => new { x.did, x.siid, x.piid })]);
    }
    protected void VerifyGetProperties(int initialId, params (int siid, int piid)[] args)
    {
        VerifyMethodWithId("get_properties", initialId, [.. args.Select(x => new { did = $"{x.siid}-{x.piid}", x.siid, x.piid })]);
    }
    protected void VerifySetProperties(int siid, int piid, string did, object value, int initialId = 2)
    {
        VerifyMethodWithId("set_properties", initialId, new { did, siid, piid, value });
    }
    protected void VerifySetProperties(int siid, int piid, object value, int initialId = 2)
    {
        VerifySetProperties(siid, piid, $"set-{siid}-{piid}", value, initialId);
    }
    protected void SetupCallAction()
    {
         _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => System.Text.Json.Nodes.JsonNode.Parse(s)["method"].GetValue<string>() == "action")))
            .ReturnsAsync(new { result = new { code = 0}, id = 1}.ToJson());
    }
    protected void VerifyCallAction(int siid, int aiid, int initialId = 2)
    {
        _miioTransport
            .Verify(x => x.SendMessageAsync(new
            {
                id = initialId,
                method = "action",
                @params = new { did = $"call-{siid}-{aiid}", siid, aiid, @in = new string[] { } }
            }.ToJson()), Times.Once());
    }
}
