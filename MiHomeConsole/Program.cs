using System;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeConsole;
public class Program
{
    public static void Main()
    {
        using var gw2 = new XiaomiGateway2("192.168.1.13", "8ac2ffaa10eec2469b5d585f34dd1663");
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