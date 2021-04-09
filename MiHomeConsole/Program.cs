using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MiHomeLib;
using MiHomeLib.Devices;

namespace MiHomeConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var miHome = new MiHome("7c4mx86hn658f0f3");

            miHome.OnSocketPlug += (_, socketPlug) =>
            {
                if (socketPlug.Sid == "158d00015dc662") // sid of specific device
                {
                    Console.WriteLine(socketPlug); // sample output Status: on, Inuse: 1, Load Power: 2.91V, Power Consumed: 37049W, Voltage: 3.6V
                    socketPlug.TurnOff();
                    Task.Delay(5000).Wait();
                    socketPlug.TurnOn();
                }
            };

            Console.ReadLine();

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
            //using var miHome = new MiHome("7c4mx86hn658f0f3");
            //miHome.OnGateway += (_, gateway) =>
            //{
            //    gateway.PlayCustomSound(10_002, 35);
            //    Task.Delay(5000).Wait();
            //    gateway.SoundsOff();
            //};


            //using var miHome = new MiHome();

            //miHome.OnAnyDevice += (_, device) =>
            //{
            //    Console.WriteLine($"{device.Sid}, {device.GetType()}, {device}"); // all discovered devices
            //};

            //var airHumidifier = new AirHumidifier("192.168.1.26", "5d3a2f018c90097a850558c35c953b77");
            //Console.WriteLine(airHumidifier);

            //AirHumidifier.OnDiscovered += (_, args) =>
            //{
            //    Console.WriteLine($"{args.Ip} {args.Token}");
            //};

            //AirHumidifier.DiscoverDevices();

            //var gw = new MiioGateway("192.168.1.12", "8ac2ffaa10eec2469b5d585f34dd1663");
            //gw.SetArmingOn();
            //Task.Delay(1000).Wait();
            //gw.SetArmingOff();
            //gw.SetArmingWaitTime(30);
            //gw.SetArmingBlinkingTime(10);
            //gw.SetArmingVolume(10);
            //Console.WriteLine(gw.GetArmingLastTimeTriggeredTimestamp());

            //Console.ReadLine();
        }
    }
}
