using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway;

public class MultimodeGateway2 : MultimodeGateway
{
    public MultimodeGateway2(string ip, string token, int port = 1883) : base(ip, token, port)
    {
        _logger = _loggerFactory.CreateLogger<MultimodeGateway2>();
        _devicesDiscoverer = new MultimodeGateway2DevicesDiscoverer(ip, 23);
    }   

    //TODO: Implement ToString() function showing gw model info
}
