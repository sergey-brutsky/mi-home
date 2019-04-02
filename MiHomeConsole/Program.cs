using System;
using System.Threading.Tasks;
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
            using (var miHome = new MiHome())
            {
                miHome.Devices.Subscribe(device =>
                {
                    // First way to receive change events
                    // Subscribe on Changes observable
                    Console.WriteLine($"New device found: {device.Sid}, {device.GetType()}, {device}");
                    device.Changes.Subscribe(d =>
                    {
                        Console.WriteLine($"Device changed: {device.Sid}, {device.GetType()}, {device}");
                    });

                    // Second way: listen for events from specific types of devices
                    // (or on specific devices, if you know and get device by sid or name map)
                    if (!(device is DoorWindowSensor th)) return;
                    th.OnClose += (sender, a) =>
                    {
                        var dev = (DoorWindowSensor)sender;
                        Console.WriteLine($"New event on close {dev.Sid}, {dev.GetType()}, {dev}");
                    };
                    th.OnOpen += (sender, a) =>
                    {
                        var dev = (DoorWindowSensor)sender;
                        Console.WriteLine($"New event on open {dev.Sid}, {dev.GetType()}, {dev}");
                    };
                });

                Console.ReadLine();
            }
        }

    }
}
