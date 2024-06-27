using System;
using MiHomeLib;

namespace MiHomeConsole;
public class Program
{
    public static void Main()
    {
        using var gw3 = new XiaomiGateway3("<gateway ip>", "<gateway token>");
        {
            gw3.OnDeviceDiscovered += gw3SubDevice =>
            {
                Console.WriteLine(gw3SubDevice.ToString());
            };

            gw3.DiscoverDevices();
        }
        
        Console.ReadLine();
    }
}