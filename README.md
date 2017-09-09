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

Check example bellow how to get all devices and know their sids.

## Usage example

Gettings all devices in the network

```csharp
public static void Main(string[] args)
{
    // pwd of your gateway (optional, needed only to send commands to your devices) 
    // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
    var platform = new Platform("7c4mx86hn658f0f3");

    Thread.Sleep(2000);

    foreach (var miHomeDevice in platform.GetDevices())
    {
        Console.WriteLine(miHomeDevice); // all discovered devices
    }
    
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

### 4. Motion sensor
![](http://i1.mifile.cn/a1/T1bFJ_B4Jv1RXrhCrK!200x200.jpg)

```csharp
var motionSensor = platform.GetDevicesByType<MotionSensor>().First();

motionSensor.OnMotion += (_, __) =>
{
    Console.WriteLine($"{DateTime.Now}: Motion detected !");
};

motionSensor.OnNoMotion += (_, e) =>
{
    Console.WriteLine($"{DateTime.Now}: No motion for {e.Seconds}s !");
};
```

### 5.  Door/Window sensor
![](http://i1.mifile.cn/a1/T1zXZgBQLT1RXrhCrK!200x200.jpg)

```csharp
var windowSensor = platform.GetDevicesByType<DoorWindowSensor>().First();

windowSensor.OnOpen += (_, __) =>
{
    Console.WriteLine($"{DateTime.Now}: Window opened !");
};

windowSensor.OnClose += (_, __) =>
{
    Console.WriteLine($"{DateTime.Now}: Window closed !");
};
```
When I buy more devices I will update library
