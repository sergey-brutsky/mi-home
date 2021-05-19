# C# Library for using xiaomi smart gateway in your automation scenarious

[![Build project](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml/badge.svg)](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml)
[![Nuget](https://buildstats.info/nuget/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sergey-brutsky/mi-home/blob/master/LICENSE.md)

This library provides simple and flexible C# API for Xiaomi Mi Home devices.  

Currently supports **only Gateway version 2 (DGNWG02LM)**, Air Humidifier (zhimi.humidifier.v1), Mi Robot vacuum (rockrobo.vacuum.v1) and several sensors. See the pictures below.

![smart-home](https://user-images.githubusercontent.com/5664637/118375593-46751980-b5cb-11eb-81f9-93b095401737.jpeg)

![humidifier](https://user-images.githubusercontent.com/5664637/102880937-25b1f900-445d-11eb-83e4-1f96830510d6.jpg)
![mirobot](https://user-images.githubusercontent.com/5664637/118375624-7de3c600-b5cb-11eb-8887-772795f7fbf5.jpeg)

![gateway](https://user-images.githubusercontent.com/5664637/32080159-d2fbd29a-bab6-11e7-9ef8-e18c048fd5fe.jpg)
![temperature_sensor](https://user-images.githubusercontent.com/5664637/32080111-88c9a058-bab6-11e7-9d73-82dd77e362ae.jpg)
![socket_plug](https://user-images.githubusercontent.com/5664637/32080247-4b007520-bab7-11e7-9e0a-83e01ee37b8e.jpg)
![motion_sensor](https://user-images.githubusercontent.com/5664637/32079992-db2366d2-bab5-11e7-9f5f-d9bf711f261f.jpg)
![motion_sensor_2](./images/MotionSensor2.jpg)
![door_window_sensor](https://user-images.githubusercontent.com/5664637/32079914-83947b22-bab5-11e7-8f5c-43d07ca82022.jpg)
![aqara_door_window_sensor](./images/ContactSensor2.jpg)
![water_sensor](https://user-images.githubusercontent.com/5664637/32079774-d6bdd9d4-bab4-11e7-8a48-5c2b7ea978c9.jpg)
![smoke_sensor](https://user-images.githubusercontent.com/5664637/32079813-05bfab9a-bab5-11e7-9416-2227e167f0ab.jpg)
![switch](https://user-images.githubusercontent.com/5664637/37819616-233b087e-2e8f-11e8-8558-7e47137705d4.jpg)
![wired wall switch](https://user-images.githubusercontent.com/5664637/37880344-6dc7b066-308f-11e8-80b1-1b39ef973acf.jpg)
![sensor_weather](https://user-images.githubusercontent.com/5664637/37911004-9687dafc-3117-11e8-9e82-a6823da8da0b.jpg)
![wireless dual wall switch](https://user-images.githubusercontent.com/5664637/63649478-eaa79480-c746-11e9-94ff-092814f62c6f.jpg)
![aqara_cube_sensor](./images/MagicSquare.jpg)


## Table of Contents
1. [Installation](#installation)
2. [Setup Gateway](#setup-gateway)
3. [Basic scenario](#basic-scenario)
4. [Supported devices](#supported-devices)
    - 4.1 [Gateway](#gateway)
 	   - 4.1.1 [Gateway radio](#gateway-radio)    
    - 4.2 [Temparature & humidity sensor](#th-sensor)
    - 4.3 [Socket plug](#socket-plug)
    - 4.4 [Motion sensor](#motion-sensor)
    - 4.5 [Door/Window sensor](#door-window-sensor)
    - 4.6 [Water leak sensor](#water-sensor)
    - 4.7 [Smoke sensor](#smoke-sensor)
    - 4.8 [Wireless dual wall switch](#dual-wall-sensor)
    - 4.9 [Aqara cube](#aqara-cube)
    - 4.10 [Air humidifier](#air-humidifier)
    - 4.11 [Mi Robot Vacuum](#mi-robot-v1)

## <a name="installation">Installation</a>
via nuget package manager
```nuget
Install-Package MiHomeLib
```
or
```nuget
dotnet add package MiHomeLib
```
or install via [GitHub packages](https://github.com/sergey-brutsky/mi-home/packages/540443)

## <a name="setup-gateway">Setup Gateway</a>

Before using this library you should setup **development mode** on your gateway, [instructions how to do this](https://www.domoticz.com/wiki/Xiaomi_Gateway_(Aqara)).\
This mode allows to work with the gateway via UDP multicast protocol.


**Warning 1**: 
If you bought a newer revision of Mi Home Gateway (labels in a circle) 
<img src="https://user-images.githubusercontent.com/5664637/75097306-451c9300-55ba-11ea-90f9-f99b5ea883c1.png" width="450">

It could be possible that ports on your gateway required for UDP multicast traffic are **closed**.\
Before using this library **ports must be opened**. [Check this instruction](https://community.openhab.org/t/solved-openhab2-xiaomi-mi-gateway-does-not-respond/52963/114).

**Warning 2**: Mi Home Gateway uses udp multicast for messages handling, so your app **must** be hosted in the same LAN as your gateway.
If it is not you **have to** use multicast routers like [udproxy](https://github.com/pcherenkov/udpxy) or [igmpproxy](https://github.com/pali/igmpproxy) or [vpn bridging](https://forums.openvpn.net/viewtopic.php?t=21509).

**Warning 3**: If your app is running on windows machine, make sure that you disabled virtual network adapters like VirtualBox, Hyper-V, Npcap, pcap etc.
Because these adapters may prevent proper work of multicast traffic between your machine and gateway

## <a name="basic-scenario">Basic scenario</a>
Get all devices in the network

```csharp
public static void Main(string[] args)
{
    // gateway password is optional, needed only to send commands to your devices
    // gateway sid is optional, use only when you have 2 gateways in your LAN
    // using var miHome = new MiHome("gateway password", "gateway sid");
    using var miHome = new MiHome();
   
    miHome.OnAnyDevice += (_, device) =>
    {
        Console.WriteLine($"{device.Sid}, {device.GetType()}, {device}"); // all discovered devices
    };

    Console.ReadLine();
}
```
## <a name="usage-examples">Supported devices</a>

### 1. <a name="gateway">Gateway</a>

![gateway](https://user-images.githubusercontent.com/5664637/32080159-d2fbd29a-bab6-11e7-9ef8-e18c048fd5fe.jpg)

```csharp
using var miHome = new MiHome("gateway password here"); // here we using developers api 

miHome.OnGateway += (_, gateway) =>
{
    gateway.EnableLight(); // by default this is "white" light
    Task.Delay(3000).Wait();
    gateway.DisableLight(); // light off
    Task.Delay(3000).Wait();
    gateway.EnableLight(255, 0, 0, 100); // turn on "red" light with full brightness 
    Task.Delay(3000).Wait();
    gateway.DisableLight(); // light off
    Task.Delay(3000).Wait();
    gateway.PlaySound(Gateway.Sound.IceWorldPiano, 50); // play ice world piano sound on gateway with volume 50%
    Task.Delay(3000).Wait();
    gateway.SoundsOff();
    gateway.PlayCustomSound(10_002, 50); // play custom sound with volume 50%
    Task.Delay(3000).Wait();
    gateway.SoundsOff();

};
```
Yes, it is possible to upload custom sounds to your gateway and use them in various scenarios. [Check this instruction](https://smarthomehobby.com/using-the-xiaomi-door-window-sensor/).


#### 1.1 <a name="gateway-radio">Gateway Radio</a>
It is possible to add/remove/play custom radio channels in this version of gateway.

Bellow is a simple code snippet explaining how to use this feature.

```csharp
var gw = new MiioGateway("192.168.1.12", "<your gateway token here>");

var radioChannels = gw.GetRadioChannels(); // get list of available custom radio channels

foreach (var channel in radioChannels)
{
    Console.WriteLine(channel);
}

gw.AddRadioChannel(1025, "http://192.168.1.1/my-playlist.m3u8"); // add custom radio channel
Task.Delay(1000).Wait();
gw.PlayRadio(1024, 50); // play newly-added channel with volume 50%
Task.Delay(1000).Wait();
gw.StopRadio(); // stop playing radio
Task.Delay(1000).Wait();
gw.RemoveRadioChannel(1024); // remove newly-added channel
Task.Delay(1000).Wait();
gw.RemoveAllRadioChannels(); // remove all custom radio channels
```
Async methods also supported.

**Warning 1**: Added radio channels are not persistant. Gateway may remove them from time to time.
**Warning 2**: My gateway recognizes only songs in aac format (mp3 is not supported).

Here is minimal working sample of m3u8 file that gateway recognizes and respects.
```
#EXTM3U
#EXT-X-VERSION:3
#EXT-X-MEDIA-SEQUENCE:1
#EXTINF:240,
http://192.168.1.2/test.aac
```
EXT-X-MEDIA-SEQUENCE - number of songs in your playlist.

EXTINF - track length in seconds.

http://192.168.1.2/test.aac - url to your song 
### 2. <a name="th-sensor">Temperature and humidity sensor</a>

![temperature_sensor](https://user-images.githubusercontent.com/5664637/32080111-88c9a058-bab6-11e7-9d73-82dd77e362ae.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnThSensor += (_, thSensor) =>
{
    if (thSensor.Sid == "158d000182dfbc") // sid of specific device
    {
        Console.WriteLine(thSensor); // Sample output --> Temperature: 22,19°C, Humidity: 74,66%, Voltage: 3,035V

        thSensor.OnTemperatureChange += (_, e) =>
        {
            Console.WriteLine($"New temperature: {e.Temperature}");
        };

        thSensor.OnHumidityChange += (_, e) =>
        {
            Console.WriteLine($"New humidity: {e.Humidity}");
        };
    }
};
```

### 3. <a name="socket-plug">Socket Plug (zigbee version)</a>

![socket_plug](https://user-images.githubusercontent.com/5664637/32080247-4b007520-bab7-11e7-9e0a-83e01ee37b8e.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnSocketPlug += (_, socketPlug) =>
{
    if (socketPlug.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(socketPlug); // sample output Status: on, Inuse: 1, Load Power: 2.91V, Power Consumed: 37049W, Voltage: 3.6V

        socketPlug.TurnOff();
        Task.Delay(5000).Wait();
        socketPlug.TurnOn();
    }
};
```

### 4. <a name="motion-sensor">Motion sensor or Aqara motion sensor</a>

![motion_sensor](https://user-images.githubusercontent.com/5664637/32079992-db2366d2-bab5-11e7-9f5f-d9bf711f261f.jpg)
![motion_sensor_2](./images/MotionSensor2.jpg)

```csharp
using var miHome = new MiHome();

//miHome.OnAqaraMotionSensor += (_, motionSensor) =>
miHome.OnMotionSensor += (_, motionSensor) =>
{
    if (motionSensor.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(motionSensor); // sample output Status: motion, Voltage: 3.035V, NoMotion: 0s

        motionSensor.OnMotion += (_, __) =>
        {
            Console.WriteLine($"{DateTime.Now}: Motion detected !");
        };

        motionSensor.OnNoMotion += (_, e) =>
        {
            Console.WriteLine($"{DateTime.Now}: No motion for {e.Seconds}s !");
        };
    }
};
```

### 5. <a name="door-window-sensor">Door/Window sensor or Aqara open/close sensor</a>

![door_window_sensor](https://user-images.githubusercontent.com/5664637/32079914-83947b22-bab5-11e7-8f5c-43d07ca82022.jpg)
![aqara_door_window_sensor](./images/ContactSensor2.jpg)

```csharp
using var miHome = new MiHome();

//miHome.OnAqaraOpenCloseSensor += (_, windowSensor) =>
miHome.OnDoorWindowSensor += (_, windowSensor) =>
{
    if (windowSensor.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(windowSensor); // sample output Status: close, Voltage: 3.025V

        windowSensor.OnOpen += (_, __) =>
        {
            Console.WriteLine($"{DateTime.Now}: Window opened !");
        };

        windowSensor.OnClose += (_, __) =>
        {
            Console.WriteLine($"{DateTime.Now}: Window closed !");
        };

    }
};
```

### 6. <a name="water-sensor">Water leak sensor</a>

![water_sensor](https://user-images.githubusercontent.com/5664637/31301235-2d6403ee-ab01-11e7-914a-80641e3ba2bf.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnWaterLeakSensor += (_, waterLeakSensor) =>
{
    if (waterLeakSensor.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(waterLeakSensor); // Status: no_leak, Voltage: 3.015V

        waterLeakSensor.OnLeak += (_, __) =>
        {
            Console.WriteLine("Water leak detected !");
        };

        waterLeakSensor.OnNoLeak += (_, __) =>
        {
            Console.WriteLine("NO leak detected !");
        };

    }
};
```

### 7. <a name="smoke-sensor">Smoke sensor</a>

![smoke_sensor](https://user-images.githubusercontent.com/5664637/32071412-e3db3e76-ba97-11e7-840c-1d901df4b84f.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnSmokeSensor += (_, smokeSensor) =>
{
    if (smokeSensor.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(smokeSensor); // sample output Alarm: off, Density: 0, Voltage: 3.075V

        smokeSensor.OnAlarm += (_, __) =>
        {
            Console.WriteLine("Smoke detected !");
        };

        smokeSensor.OnAlarmStopped += (_, __) =>
        {
            Console.WriteLine("Smoke alarm stopped");
        };

        smokeSensor.OnDensityChange += (_, e) =>
        {
            Console.WriteLine($"Density changed {e.Density}");
        };
    }
};
```

### 8. <a name="dual-wall-sensor">Wireless dual wall switch</a>

![wireless dual wall switch](https://user-images.githubusercontent.com/5664637/63649478-eaa79480-c746-11e9-94ff-092814f62c6f.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnWirelessDualWallSwitch += (_, wirelessDualSwitch) =>
{
    if (wirelessDualSwitch.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(wirelessDualSwitch);

        wirelessDualSwitch.OnLeftClick += (_) =>
        {
            Console.WriteLine("Left button clicked !");
        };

        wirelessDualSwitch.OnRightDoubleClick += (_) =>
        {
            Console.WriteLine("Right button double clicked !");
        };

        wirelessDualSwitch.OnLeftLongClick += (_) =>
        {
            Console.WriteLine("Left button long clicked !");
        };

    }
};
```

### 9.  <a name="aqara-cube">Aqara cube</a>

![aqara_cube_sensor](./images/MagicSquare.jpg)

```csharp
using var miHome = new MiHome();

miHome.OnAqaraCubeSensor += (_, aqaraQube) =>
{
    if (aqaraQube.Sid == "158d00015dc6cc") // sid of specific device
    {
        Console.WriteLine(aqaraQube);

        aqaraQube.OnStatusChanged += (sender, eventArgs) =>
        {
            Console.WriteLine($"{sender} | {eventArgs.Status}");
        };

    }
};
```

### 10. <a name="air-humidifier">Air Humidifier</a>
![humidifier](https://user-images.githubusercontent.com/5664637/102878695-b71f6c00-4459-11eb-92c1-518c57b34683.jpg)

Before using the library you need to know IP and TOKEN of your air humidifier.
If you don't know these parameters try to use the following code in order to discover air humidifiers in your LAN
```csharp
AirHumidifier.OnDiscovered += (_, humidifier) =>
{
    Console.WriteLine($"ip: {humidifier.Ip}, token: {humidifier.Token}");
    // sample output ip: 192.168.1.5, token: 4a3a2f017b70097a850558c35c953b55
};

AirHumidifier.DiscoverDevices();
```
If your device hides his token follow [these instructions](https://github.com/Maxmudjon/com.xiaomi-miio/blob/master/docs/obtain_token.md) in order to extract it.

Basic scenario

```csharp
var airHumidifier = new AirHumidifier("<ip here>", "<token here>");
Console.WriteLine(airHumidifier);
/* sample output
Power: on
Mode: high
Temperature: 32.6 °C
Humidity: 34%
LED brightness: bright
Buzzer: on
Child lock: off
Target humidity: 50%
Model: zhimi.humidifier.v1
IP Address:192.168.1.5
Token: 4a3a2f017b70097a850558c35c953b55
*/
```
Functions
```csharp
var airHumidifier = new AirHumidifier("<ip here>", "<token here>");
airHumidifier.PowerOn(); // power on
airHumidifier.PowerOff(); // power off
airHumidifier.SetMode(AirHumidifier.Mode.High); // set fan mode high/medium/low
airHumidifier.GetTemperature(); // get temperature
airHumidifier.GetHumidity(); // get humidity
airHumidifier.SetBrightness(AirHumidifier.Brightness.Bright); // set brighness bright/dim/off
airHumidifier.BuzzerOn(); // set buzzer sound on
airHumidifier.BuzzerOff(); // set buzzer sound off
airHumidifier.ChildLockOn(); // set child lock on
airHumidifier.ChildLockOff(); // set child lock oаа
airHumidifier.GetTargetHumidity(); // get humidity limit 20/30/40/50/60/70/80 %
```
Async versions of the operations above also supported.

### 11. <a name="mi-robot-v1">Mi Robot Vacuum</a>
![mirobot](https://user-images.githubusercontent.com/5664637/118375492-a6b78b80-b5ca-11eb-86d3-3b9065ac3892.jpeg)

Before using the library you need to know IP and TOKEN of your Mi Robot.

If you don't know these parameters try to use the following code in order to discover **mi robots** in your LAN
```csharp
MiRobotV1.OnDiscovered += (_, e) =>
{
	Console.WriteLine($"{e.Ip}, {e.Serial}, {e.Type}, {e.Token}");
};

MiRobotV1.DiscoverDevices()
```
If your device hides his token (you get 'ffffffffffffffffffffffffffffffff' instead of token) follow [these instructions](https://github.com/Maxmudjon/com.xiaomi-miio/blob/master/docs/obtain_token.md) in order to extract it.

Supported methods
```csharp
var miRobot = new MiRobotV1("<ip here>", "<token here>");
miRobot.Start(); // start the clean up
miRobot.Stop(); // stop the clean up
miRobot.Pause(); // pause the clean up
miRobot.Spot(); // start spot clean up
miRobot.Home(); // go back to the base station
miRobot.FindMe(); // tell the robot to give a voice
```
Async versions of the operations above also supported.

**Warning**: 
Mi Robot stores client requests in memory and doesn't allow to send request with the same client id twice.

It means that if you run the code snippet bellow twice.
```csharp
var miRobot = new MiRobotV1("<ip here>", "<token here>");
miRobot.Start(); // start the clean up
```
The second attempt will fail.
Work around is to set client id manually (usually increasing to 1 works)
```csharp
var miRobot = new MiRobotV1("<ip here>", "<token here>", 2); // client request id is set to 2
miRobot.Start(); // start the clean up
```
