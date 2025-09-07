using System.Linq;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.Transport;
using Moq;

namespace MiHomeUnitTests;

public class MiioDeviceBase
{
    protected Mock<IMiioTransport> _miioTransport = new();
    protected void VerifyMethod(string method, params object[] args)
    {
        VerifyMethod(method, 1, args);
    }
    protected void VerifyMethod(string method, int id, params object[] args)
    {
        _miioTransport
            .Verify(x => x.SendMessage(new { id, method, @params = args}.ToJson()), Times.Once());
    }
    protected void VerifyMethodAsync(string method, params object[] args)
    {
        _miioTransport
            .Verify(x => x.SendMessageAsync(new { id = 1, method, @params = args }.ToJson()), Times.Once());
    }
    protected void SendResultMethodAsync(string method, params object[] args)
    {
        SendResultMethodAsync(method, 1, args);
    }
    protected void SendResultMethodAsync(string method, int id, params object[] args)
    {
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains(method))))
            .Returns(Task.FromResult(new { result = args, id}.ToJson()));
    }
    protected void SendResultMethod(string method, params object[] args)
    {
        SendResultMethod(method, 1, args);
    }
    protected void SendResultMethod(string method, int id, params object[] args)
    {
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains(method))))
            .Returns(new { result = args, id}.ToJson());
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
        VerifyMethod("get_properties", initialId, [.. args.Select(x => new { x.did, x.siid, x.piid })]);
    }
    protected void VerifyGetProperties(int initialId, params (int siid, int piid)[] args)
    {
        VerifyMethod("get_properties", initialId, [.. args.Select(x => new { did = $"{x.siid}-{x.piid}", x.siid, x.piid })]);
    }
    protected void VerifySetProperties(int siid, int piid, string did, object value, int initialId = 2)
    {
        VerifyMethod("set_properties", initialId, new { did, siid, piid, value });
    }
    protected void VerifySetProperties(int siid, int piid, object value, int initialId = 2)
    {
        VerifySetProperties(siid, piid, $"set-{siid}-{piid}", value, initialId);
    }
    protected void SetupCallAction()
    {
         _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("action"))))
            .Returns(new { result = new { code = 0}, id = 1}.ToJson());
    }
    protected void VerifyCallAction(int siid, int aiid, int initialId = 2)
    {
        _miioTransport
            .Verify(x => x.SendMessage(new
            {
                id = initialId,
                method = "action",
                @params = new { did = $"call-{siid}-{aiid}", siid, aiid, @in = new string[] { } }
            }.ToJson()), Times.Once());
    }
}