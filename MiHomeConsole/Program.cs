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
            var platform = new Platform("pwd"); // pwd of your lumi gateway

            var thSensor = new ThSensor("158d0001826509"); // this is sid of your sensor

            platform.AddDeviceToWatch(thSensor);

            platform.Connect();

            Thread.Sleep(5000); // Waiting for some time when sensor answers via udp multicast

            platform.Disconnect();

            Console.WriteLine($"Temperature: {thSensor.Temperature}, Humidity: {thSensor.Humidity}");

            Console.ReadKey();
        }
    }
}
