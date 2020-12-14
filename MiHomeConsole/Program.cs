using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MiHomeLib;
using MiHomeLib.Devices;

namespace MiHomeConsole
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            //Action<ILoggingBuilder> loggingBuilder =
            //    builder => builder.AddConsole(x =>
            //    {
            //        x.DisableColors = true;
            //        x.Format = ConsoleLoggerFormat.Systemd;
            //        x.TimestampFormat = " yyyy-MM-d [HH:mm:ss] - ";
            //    });

            //MiHome.LoggerFactory = LoggerFactory.Create(loggingBuilder);
            //MiHome.LogRawCommands = true;
            
            // pwd of your gateway (optional, needed only to send commands to your devices) 
            // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
            //using (var miHome = new MiHome("pwd", "sid"))
            using (var miHome = new MiHome())
            {
                Task.Delay(5000).Wait();

                foreach (var miHomeDevice in miHome.GetDevices())
                {
                    Console.WriteLine($"{miHomeDevice.Sid}, {miHomeDevice.GetType()}, {miHomeDevice}"); // all discovered devices
                }

                Console.ReadLine();
            }
        }
    }
}
