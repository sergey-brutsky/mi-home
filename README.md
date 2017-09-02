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

## Getting started

You need to know sid of all you smart devices.

Here is an instruction how to get to know them  --> TBD

## Usage examples

Getting temperature and humidity

```csharp
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
```
### Supported devices

### 1. Gateway
![](http://i1.mifile.cn/a1/T19eL_Bvhv1RXrhCrK!200x200.jpg)

### 2. Temperature and humidity sensor
![](http://i1.mifile.cn/a1/T1xKYgBQhv1R4cSCrK!200x200.png)

### 3. Socket Plug
![](http://i1.mifile.cn/a1/T1kZd_BbLv1RXrhCrK!200x200.jpg)

When I buy more devices I will update library
