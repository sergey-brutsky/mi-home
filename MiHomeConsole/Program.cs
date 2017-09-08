using System;
using System.Threading;
using MiHomeLib;
using MiHomeLib.Devices;

namespace MiHomeConsole
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // pwd of your gateway (optional, needed only to send commands to your devices) 
            // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
            var platform = new Platform("7c4mx86hn658f0f3");
            
            Thread.Sleep(2000);

            foreach (var miHomeDevice in platform.GetDevices())
            {
                Console.WriteLine(miHomeDevice); // all discovered devices
            }

            var thSensor = platform.GetDeviceBySid<ThSensor>("158d000182dfbc"); // get specific device

            Console.WriteLine(thSensor);

            var gateway = platform.GetGateway();
            
            gateway.EnableLight(); // "white" light by default
            Thread.Sleep(5000);
            gateway.DisableLight();
            
            Console.ReadKey();
        }
    }
}
