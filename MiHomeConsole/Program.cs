using System;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway3;

namespace MiHomeConsole;
public class Program
{
    public static void Main()
    {       
        using var gw3 = new XiaomiGateway3("ip", "token");
        {
            gw3.OnDeviceDiscoveredAsync += d =>
            {
                Console.WriteLine(d.ToString());
                return Task.CompletedTask;
            };

            gw3.DiscoverDevices();
        }
        Console.ReadLine();
    }
}