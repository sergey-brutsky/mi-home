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

            //socketPlug.TurnOff();
            //Thread.Sleep(5000);
            //socketPlug.TurnOn();
            
            //gateway.EnableLight(); // "white" light by default
            //Thread.Sleep(5000);
            //gateway.DisableLight();

            platform.Disconnect();

            Console.WriteLine($"TH sensor temperature: {thSensor.Temperature}, humidity: {thSensor.Humidity}, voltage: {thSensor.Voltage}");
            Console.WriteLine($"Socket plug status: {socketPlug.Status}, inuse: {socketPlug.Inuse}, load power: {socketPlug.LoadPower}, power consumed: {socketPlug.PowerConsumed}");
            Console.WriteLine($"Gateway rgb: {gateway.Rgb}, illumination: {gateway.Illumination}, proto: {gateway.ProtoVersion}");

            Console.ReadKey();
        }
    }
}
