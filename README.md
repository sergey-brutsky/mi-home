# C# Library for using xiaomi smart gateway in your automation scenarious

This library provides simple and flexible C# API for Xiaomi Mi Home devices.  

Currently supports only Xiaomi Smart Gateway 2 device and several sensors. Please see the pictures below.
![](https://xiaomi-mi.com/uploads/CatalogueImage/xiaomi-mi-smart-home-kit-00_13743_1460032023.jpg)

*Warning: This is experimental version. It may be very unstable*
## Installation
```
git clone git@github.com:sergey-brutsky/mi-home.git
```
## Setup Gateway
Before starting to use this library you should setup development mode on your gateway.

Here is instruction --> https://www.domoticz.com/wiki/Xiaomi_Gateway_(Aqara)

*Warning: Mi Home Gateway uses udp multicast for messages handling.
So your app **must** be hosted in the same LAN as your gateway or you have to use multicast routers like [udproxy](https://github.com/pcherenkov/udpxy) or [igmpproxy](https://github.com/pali/igmpproxy) or [vpn briding](https://forums.openvpn.net/viewtopic.php?t=21509)

## Getting started

You need to know sid of all you smart devices.

Here is an instruction how to get to know them  --> TBD

## Usage examples

Getting temperature and humidity

```csharp
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
```
### Supported devices

### 1. Gateway
![](http://i1.mifile.cn/a1/T19eL_Bvhv1RXrhCrK!200x200.jpg)

### 2. Temperature and humidity sensor
![](http://i1.mifile.cn/a1/T1xKYgBQhv1R4cSCrK!200x200.png)

### 3. Socket Plug
![](http://i1.mifile.cn/a1/T1kZd_BbLv1RXrhCrK!200x200.jpg)

When I buy more devices I will update library
