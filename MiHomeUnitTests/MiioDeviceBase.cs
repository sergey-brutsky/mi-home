using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MiHomeLib.Transport;
using Moq;

namespace MiHomeUnitTests;

public class MiioDeviceBase
{
    protected Mock<IMiioTransport> _miioTransport; 
    public MiioDeviceBase()
    {
        _miioTransport = new Mock<IMiioTransport>();
    }
    protected static string ToJson(Dictionary<string, object> data) => JsonSerializer.Serialize(data);
    protected static string ResultOkJson(int ok = 1)
    {
        return ToJson(new()
            {
                { "result", new[] { "ok" } },
                { "id", ok },
            });
    }
    protected void VerifyMethod(string method, params object[] args)
    {
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new()
            {
                { "id", 1 },
                { "method", method },
                { "params", args },
            })), Times.Once());
    }
    protected void VerifyMethodAsync(string method, params object[] args)
    {
        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new()
            {
                { "id", 1 },
                { "method", method },
                { "params", args },
            })), Times.Once());
    }     
    protected void SendResultMethodAsync(string method, params object[] args)
    {
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains(method))))
            .Returns(Task.FromResult(ToJson(new()
            {
                { "result", args},
                { "id", 1}
            })));
    }
    protected void SendResultMethod(string method, params object[] args)
    {
        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains(method))))
            .Returns(ToJson(new()
            {
                { "result", args},
                { "id", 1}
            }));
    }  
}