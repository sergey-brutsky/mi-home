using System;
using System.Threading.Tasks;
using MiHomeLib.MultimodeGateway;

namespace MiHomeConsole;
public class Program
{
    public static void Main()
    {       
        using var gw = new MultimodeGateway("ip", "token");
        {
            gw.OnDeviceDiscoveredAsync += d =>
            {
                Console.WriteLine(d.ToString());
                return Task.CompletedTask;
            };

            gw.DiscoverDevices();
        }
        Console.ReadLine();
    }
}