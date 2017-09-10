using System;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib;

namespace MiHomeConsole
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // pwd of your gateway (optional, needed only to send commands to your devices) 
            // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
            using (var miHome = new MiHome("7c4mx86hn658f0f3"))
            {
                Thread.Sleep(5000);

                foreach (var miHomeDevice in miHome.GetDevices())
                {
                    Console.WriteLine(miHomeDevice); // all discovered devices
                }

                var gw = miHome.GetGateway();
                gw.EnableLight();
                Task.Delay(3000).Wait();
                gw.DisableLight();

                Console.ReadLine();
            }
        }
    }
}
