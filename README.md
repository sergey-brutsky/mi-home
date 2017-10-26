# C# Library for using xiaomi smart gateway in your automation scenarious

This library provides simple and flexible C# API for Xiaomi Mi Home devices.  

Currently supports only Xiaomi Smart Gateway 2 device and several sensors. Please see the pictures below.
![](https://xiaomi-mi.com/uploads/CatalogueImage/xiaomi-mi-smart-home-kit-00_13743_1460032023.jpg)

![](http://i1.mifile.cn/a1/T19eL_Bvhv1RXrhCrK!200x200.jpg)
![](http://i1.mifile.cn/a1/T1xKYgBQhv1R4cSCrK!200x200.png)
![](http://i1.mifile.cn/a1/T1kZd_BbLv1RXrhCrK!200x200.jpg)
![](http://i1.mifile.cn/a1/T1bFJ_B4Jv1RXrhCrK!200x200.jpg)
![door_window_sensor](https://user-images.githubusercontent.com/5664637/32079914-83947b22-bab5-11e7-8f5c-43d07ca82022.jpg)
![water_sensor](https://user-images.githubusercontent.com/5664637/32079774-d6bdd9d4-bab4-11e7-8a48-5c2b7ea978c9.jpg)
![smoke_sensor](https://user-images.githubusercontent.com/5664637/32079813-05bfab9a-bab5-11e7-9416-2227e167f0ab.jpg)

**Warning**: This is experimental version. It may be very unstable.
## Installation
via nuget package manager
```nuget
Install-Package MiHomeLib
```
## Setup Gateway
Before starting to use this library you should setup development mode on your gateway.

Here is instruction --> https://www.domoticz.com/wiki/Xiaomi_Gateway_(Aqara)

**Warning**: Mi Home Gateway uses udp multicast for messages handling.
So your app **must** be hosted in the same LAN as your gateway or you have to use multicast routers like [udproxy](https://github.com/pcherenkov/udpxy) or [igmpproxy](https://github.com/pali/igmpproxy) or [vpn briding](https://forums.openvpn.net/viewtopic.php?t=21509)

## Usage examples

Get all devices in the network

```csharp
public static void Main(string[] args)
{
    // pwd of your gateway (optional, needed only to send commands to your devices) 
    // and sid of your gateway (optional, use only when you have 2 gateways in your LAN)
    using (var miHome = new MiHome("7c4mx86hn658f0f3"))
    {
        Task.Delay(5000).Wait();

        foreach (var miHomeDevice in miHome.GetDevices())
        {
            Console.WriteLine(miHomeDevice); // all discovered devices
        }

        Console.ReadLine();
    }
}
```
Get devices by name if you already know sid

```csharp
public static void Main(string[] args)
{

    var map = new Dictionary<string, string>
    {
        { "158d0001826509", "T&H sensor living room"}
    };

    using (var miHome = new MiHome(map))
    {
        Task.Delay(5000).Wait();

        var thSensor = miHome.GetDeviceByName<ThSensor>("T&H sensor living room");

        Console.WriteLine(thSensor);

        Console.ReadLine();
    }
}
```

### Supported devices

### 1. Gateway
![](http://i1.mifile.cn/a1/T19eL_Bvhv1RXrhCrK!200x200.jpg)

```csharp
var gateway = miHome.GetGateway();

Console.WriteLine(gateway); // Sample output --> Rgb: 0, Illumination: 997, ProtoVersion: 1.0.9

gateway?.EnableLight(); // "white" light by default
Thread.Sleep(5000);
gateway?.DisableLight();

gateway?.StartPlayMusic(1); // Track number 1 (tracks range is 0-8, 10-13, 20-29)
Thread.Sleep(5000);
gateway?.StopPlayMusic();
```

### 2. Temperature and humidity sensor
![](http://i1.mifile.cn/a1/T1xKYgBQhv1R4cSCrK!200x200.png)

```csharp
var thSensor = miHome.GetDeviceBySid<ThSensor>("158d000182dfbc"); // get specific device

Console.WriteLine(thSensor); // Sample output --> Temperature: 22,19°C, Humidity: 74,66%, Voltage: 3,035V

th.OnTemperatureChange += (_, e) =>
{
    Console.WriteLine($"New temperature: {e.Temperature}");
};

th.OnHumidityChange += (_, e) =>
{
    Console.WriteLine($"New humidity: {e.Humidity}");
};
```

### 3. Socket Plug
![](http://i1.mifile.cn/a1/T1kZd_BbLv1RXrhCrK!200x200.jpg)

```csharp
var socketPlug = miHome.GetDeviceBySid<SocketPlug>("158d00015dc6cc"); // get specific socket plug

Console.WriteLine(socketPlug); // Sample output --> Status: on, Inuse: 1, Load Power: 3,26V, Power Consumed: 1103W, Voltage: 3,6V

socketPlug.TurnOff();
Thread.Sleep(5000);
socketPlug.TurnOn();
```

### 4. Motion sensor
![](http://i1.mifile.cn/a1/T1bFJ_B4Jv1RXrhCrK!200x200.jpg)

```csharp
var motionSensor = miHome.GetDevicesByType<MotionSensor>().First();

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
var windowSensor = miHome.GetDevicesByType<DoorWindowSensor>().First();

windowSensor.OnOpen += (_, __) =>
{
    Console.WriteLine($"{DateTime.Now}: Window opened !");
};

windowSensor.OnClose += (_, __) =>
{
    Console.WriteLine($"{DateTime.Now}: Window closed !");
};
```
### 5.  Water leak sensor
![water_sensor](https://user-images.githubusercontent.com/5664637/31301235-2d6403ee-ab01-11e7-914a-80641e3ba2bf.jpg)

```csharp
var waterSensor = miHome.GetDevicesByType<WaterLeakSensor>().First();

waterSensor.OnLeak += (s, e) =>
{
    Console.WriteLine("Water leak detected !");
};

waterSensor.OnNoLeak += (s, e) =>
{
    Console.WriteLine("NO leak detected !");
};
```
### 6.  Smoke sensor
![smoke_sensor](https://user-images.githubusercontent.com/5664637/32071412-e3db3e76-ba97-11e7-840c-1d901df4b84f.jpg)

```csharp
var smokeSensor = miHome.GetDevicesByType<SmokeSensor>().First();

smokeSensor.OnAlarm += (_, __) =>
{
    Console.WriteLine("Smoke detected !");
};

smokeSensor.OnAlarmStopped += (_, __) =>
{
    Console.WriteLine("Smoke alarm stopped");
};

smokeSensor.OnDensityChanged += (_, e) =>
{
    Console.WriteLine($"Density changed {e.Density}");
};
```


When I buy more devices I will update library
