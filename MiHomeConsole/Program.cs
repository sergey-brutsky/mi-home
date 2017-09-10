using System;
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

            using (var miHome = new MiHome())
            {
                Task.Delay(5000).Wait();

                foreach (var miHomeDevice in miHome.GetDevices())
                {
                    Console.WriteLine(miHomeDevice); // all discovered devices
                }
                
                Console.ReadLine();
            }
        }
    }
}
