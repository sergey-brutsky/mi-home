using System;
using System.Threading;
using MiHomeLib;
using MiHomeLib.Devices;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace MiHomeConsole
{
    internal class Program
    {
        public static void Main(string[] args)
        {   
            var platform = new Platform("pwd"); // pwd of your lumi gateway

            var thSensor = new ThSensor("158d0001826509"); // this is sid of your sensor
            var socketPlug = new SocketPlug("158d00015dc662"); // this is sid of your sensor

            platform.AddDeviceToWatch(thSensor);
            platform.AddDeviceToWatch(socketPlug);

            platform.Connect();

            Thread.Sleep(5000); // Waiting for some time when sensor answers via udp multicast

            platform.Disconnect();

            Console.WriteLine($"Temperature: {thSensor.Temperature}, Humidity: {thSensor.Humidity}");
            Console.WriteLine($"Socket status: {socketPlug.Status}");

            Console.ReadKey();
        }
    }
}
