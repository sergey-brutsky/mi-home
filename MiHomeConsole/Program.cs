using System;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeConsole;
public class Program
{
    public static void Main()
    {
        using var gw2 = new XiaomiGateway2("ip", "token");
        {
            gw2.OnDeviceDiscoveredAsync += d =>
            {
                Console.WriteLine(d.ToString());
                return Task.CompletedTask;
            };

            gw2.DiscoverDevices();
        }
        Console.ReadLine();
    }
}