using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using MiHomeLib;

namespace MiHomeConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Action<ILoggingBuilder> loggingBuilder =
                builder => builder.AddSystemdConsole(x =>
                {
                    x.TimestampFormat = " yyyy-MM-d [HH:mm:ss] - ";
                });

            //MiHome.LoggerFactory = LoggerFactory.Create(loggingBuilder);
            //MiHome.LogRawCommands = true;

            // pwd of your gateway (optional, needed only to send commands to your devices) 
            // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
            //using var miHome = new MiHome("pwd", "sid")
            //using var miHome = new MiHome();

            //miHome.OnAnyDevice += (_, device) =>
            //{
            //    Console.WriteLine($"{device.Sid}, {device.GetType()}, {device}"); // all discovered devices
            //};

            MiHome3.LoggerFactory = LoggerFactory.Create(loggingBuilder);

            using var miHome = new MiHome3("192.168.5.109");

            Console.ReadLine();
        }
    }
}
