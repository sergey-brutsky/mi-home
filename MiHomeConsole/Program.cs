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
            var transport = new UdpTransport("7c4mx86hn658f0f3"); // pwd of your lumi gateway
            var platform = new Platform("34ce0088db36", transport); // sid of your gateway

            var thSensor = new ThSensor("158d0001826509"); // this is sid of your th sensor
            var socketPlug = new SocketPlug("158d00015dc662", transport); // this is sid of your socket plug
            var gateway = new Gateway("34ce0088db36", transport); // this is sid of your gateway

            platform.AddDeviceToWatch(thSensor);
            platform.AddDeviceToWatch(socketPlug);
            platform.AddDeviceToWatch(gateway);

            platform.Connect();

            gateway.EnableLight();
            Thread.Sleep(1000);
            gateway.DisableLight();

            platform.Disconnect();

            Console.WriteLine($"Temperature: {thSensor.Temperature}, Humidity: {thSensor.Humidity}");
            Console.WriteLine($"Socket status: {socketPlug.Status}");
            Console.WriteLine($"Gateway rgb: {gateway.Rgb}, illumination: {gateway.Illumination}, proto: {gateway.ProtoVersion}");

            Console.ReadLine();
        }
    }
}
