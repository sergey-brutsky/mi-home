# C# Library for using xiaomi/aqara gateways in your automation scenarious

[![Build project](https://github.com/sergey-brutsky/mi-home/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/sergey-brutsky/mi-home/actions/workflows/build-and-test.yml)
[![Tests](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/sergey-brutsky/d70d7e06eb53484b7514bfd63cec6885/raw/test_results.json)](https://github.com/sergey-brutsky/mi-home/actions/workflows/build-and-test.yml)
[![Code coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/sergey-brutsky/d70d7e06eb53484b7514bfd63cec6885/raw/code_cov.json)](https://github.com/sergey-brutsky/mi-home/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/sergey-brutsky/mi-home/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/sergey-brutsky/mi-home/actions/workflows/github-code-scanning/codeql)
[![Nuget](https://img.shields.io/nuget/v/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![Nuget](https://img.shields.io/nuget/dt/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sergey-brutsky/mi-home/blob/master/LICENSE.md)

This library provides simple and flexible C# API for Xiaomi/Aqara gateways and smart devices.

Version 3.0 includes breaking changes to device types, namespaces, network operations and event contracts. Review the migration guide before upgrading. [Migration to 3.0](https://github.com/sergey-brutsky/mi-home/wiki/Migration-3.0) · [Release summary](https://github.com/sergey-brutsky/mi-home/wiki/Release-Notes-3.0) · [Package usage](MiHomeLib/README.md)

![xiaomi-gateway-2](https://user-images.githubusercontent.com/5664637/118375593-46751980-b5cb-11eb-81f9-93b095401737.jpeg)

## Supported gateways

| Gateway | Market Model | Model |
|:--- |:---|:---|
[Xiaomi Gateway 2 China](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Gateway-2) | DGNWG02LM | lumi.gateway.v3 |
[Multimode Gateway](https://github.com/sergey-brutsky/mi-home/wiki/Multimode-Gateway) | ZNDMWG03LM | lumi.gateway.mgl03 |
[Multimode Gateway 2 Global](https://github.com/sergey-brutsky/mi-home/wiki/Multimode-Gateway-2-Global) | ZNDMWG04LM | lumi.gateway.mgl001 |
[Multimode Gateway 2 China](https://github.com/sergey-brutsky/mi-home/wiki/Multimode-Gateway-2-China) | DMWG03LM | lumi.gateway.mcn001 |
[Aqara Hub E1 China](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Hub-E1-China) | ZHWG16LM | lumi.gateway.aqcn02 |

## Supported wifi devices

| Device | Market Model | Model |
|:--- |:---|:---|
<br><img src="https://github.com/user-attachments/assets/ad53bb54-8fff-4ee4-b169-ccf2e4d76ec3" width="150"><br>[Xiaomi Mijia Smart Sterilization](https://github.com/sergey-brutsky/mi-home/wiki/Air-Humidifier-(MJJSQ03DY)) | MJJSQ03DY | zhimi.humidifier.v1 |
<br><img src="https://github.com/user-attachments/assets/03b9b58b-0fc8-4f12-a4bb-e3e2fff12adf" width="150"><br>[Xiaomi Mi Robot Vacuum Cleaner](https://github.com/sergey-brutsky/mi-home/wiki/Mi-Robot-Vacuum-(SDJQR02RR)) | SDJQR02RR | rockrobo.vacuum.v1 |
<br><img src="https://github.com/user-attachments/assets/8d619a4a-f3a9-46c8-afe9-825827cb6c4e" width="150"><br>[Mi Robot Mop 3C](https://github.com/sergey-brutsky/mi-home/wiki/Mi-Robot-Mop3C-(B106CN)) | B106CN | ijai.vacuum.v18 |
<br><img src="https://cdn.awsde0-fusion.fds.api.mi-img.com/iotweb-product-center/developer_1700550914105_9hTpU7mzlhiI0owZS4pNkPkvXZgZCpcb.png?GalaxyAccessKeyId=AKVGLQWBOVIRQ3XLEW&Expires=9223372036854775807&Signature=zc7C7QA6zoMDOgjZLTgtrIVzNQY=" width="150"><br>[Xiaomi Robot Vacuum X20 Plus](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Robot-Vacuum-X20-Plus) | X20 Plus | xiaomi.vacuum.c102gl |
<br><img src="https://github.com/user-attachments/assets/ec77bd4c-fd61-4522-8135-d0197cb32849" width="150"><br>[Xiaomi Smart Plug 2 Euro](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Smart-Plug-2-Euro-(ZNCZ302KK)) | ZNCZ302KK | cuco.plug.v2eur |
<br><img src="https://github.com/user-attachments/assets/70bfda35-7ab4-484e-9441-98b4bd5a7c40" width="150"><br>[Mijia Smart Socket 2 China](https://github.com/sergey-brutsky/mi-home/wiki/Mijia-Smart-Socket-2-China-(ZNCZ07CM)) | ZNCZ07CM | chuangmi.plug.212a01 |
<br><img src="https://github.com/user-attachments/assets/f3f3f029-80cb-4d13-8733-cea91d666412" width="150"><br>[Xiaomi Smart Air Purifier 4 Lite](https://home.miot-spec.com/spec/zhimi.airp.rma3) | AC-M17-SC | zhimi.airp.rma3 |

## Supported zigbee/ble devices

| Device support | Gateway 2 | Multimode Gateway | Multimode Gateway 2| Aqara Hub E1
|:---: |:---: |:---: |:---: |:---: |
| [Aqara Vibration Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Vibration-sensor-(DJT11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/DJT11LM.png" width="150"><br>DJT11LM | yes | yes | yes | yes
| [Xiaomi Door/Window Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Door-Window-sensor-(MCCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ01LM.png" width="150"><br>MCCGQ01LM | yes | yes | yes | yes
| [Xiaomi Door/Window Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Door-Window-sensor-2-(MCCGQ02HL))<br><img src="https://github.com/sergey-brutsky/mi-home/assets/5664637/4ac8671c-394d-4ef1-ba7a-be5f670ca103" width="150"><br>MCCGQ02HL | no | yes | yes | no
| [Aqara Door/Window Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Door-Window-sensor-(MCCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ11LM.png" width="150"><br>MCCGQ11LM | yes | yes | yes | yes
| [Xiaomi TH Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-TH-sensor-(WSDCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ01LM.png" width="150"><br>WSDCGQ01LM | yes | yes | yes | yes
| [Xiaomi TH Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-TH-sensor-2-(LYWSD03MMC))<br><img src="https://www.zigbee2mqtt.io/images/devices/LYWSD03MMC.png" width="150"><br>LYWSD03MMC | no | yes | yes | no
| [Xiaomi TH Sensor 3](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-TH-sensor-3-(MJWSD05MMC))<br><img src="https://cnbj1.fds.api.xiaomi.com/iotweb-product-center/10290.png" width="150"><br>MJWSD05MMC | no | yes | yes | no
| [Xiaomi Smart Wall Switch Single Open](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Smart-Switch-(DHKG01CM))<br><img src="https://cnbj1.fds.api.xiaomi.com/iotweb-product-center/1945.png" width="150"><br>DHKG01CM | no | yes | yes | no
| [Aqara TH Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-TH-sensor-(WSDCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ11LM.png" width="150"><br>WSDCGQ11LM | yes | yes | yes | yes
| [Aqara Water Leak Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Water-Leak-sensor-(SJCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/SJCGQ11LM.png" width="150"><br>SJCGQ11LM | yes | yes | yes | yes
| [Xiaomi Motion Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Motion-sensor-(RTCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ01LM.png" width="150"><br>RTCGQ01LM | yes | yes | yes | yes
| [Xiaomi Motion Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Motion-sensor-2-(RTCGQ02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ12LM.png" width="150"><br>RTCGQ02LM | no | yes | yes | no
| [Aqara Relay T1 EU (with N)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Relay-T1-EU-(SSM%E2%80%90U01))<br><img src="https://www.zigbee2mqtt.io/images/devices/SSM-U01.png" width="150"><br>SSM-U01 | no | yes | yes | yes
| [Aqara Relay CN](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Relay-CN-(LLKZMK11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/LLKZMK11LM.png" width="150"><br>LLKZMK11LM | no | yes | yes | yes
| [Aqara Opple Switch (2 buttons)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Opple-Switch-(2-buttons)-(WXCJKG11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG11LM.png" width="150"><br>WXCJKG11LM | no | yes | yes | yes
| [Aqara Opple Switch (4 buttons)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Opple-Switch-(4-buttons)-(WXCJKG12LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG12LM.png" width="150"><br>WXCJKG12LM | no | yes | yes | yes
| [Honeywell Smoke Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Honeywell-Smoke-Sensor-(JTYJ%E2%80%90GD%E2%80%9001LM-BW))<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-01LM/BW | yes | yes | yes | yes
| [Honeywell Smoke Alarm](https://github.com/sergey-brutsky/mi-home/wiki/Honeywell-Smoke-Alarm-(JTYJ%E2%80%90GD%E2%80%9003MI))<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-03MI | no | yes | yes | no
| [Xiaomi Wireless Button](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Wireless-Button-(WXKG01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG01LM.png" width="150"><br>WXKG01LM | yes | yes | yes | yes
| [Aqara Wireless Mini Switch](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Wireless-Mini-Switch-(WXKG11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG11LM.png" width="150"><br>WXKG11LM | yes | yes | yes | yes
| [Aqara Wireless Mini Switch E1](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Wireless-Mini-Switch-E1(WXKG20LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG20LM.png" width="150"><br>WXKG20LM | no | yes | yes | no
| [Xiaomi Plug CN](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Plug-CN-(ZNCZ02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/ZNCZ02LM.png" width="150"><br>ZNCZ02LM | yes | yes | yes | yes
| [Aqara Double Wall Switch (no N)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Double-Wall-Switch-(no-N)-(QBKG03LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/QBKG03LM.png" width="150"><br>QBKG03LM | yes | no | no | no
| [Aqara Double Wall Button CN](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Double-Wall-Button-CN-(WXKG02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG02LM_rev2.png" width="150"><br>WXKG02LM | yes | no | no | no
| [PTX Wireless Switch (BLE)](https://github.com/sergey-brutsky/mi-home/wiki/PTX-YK1-QMIMB)<br><img src="https://github.com/user-attachments/assets/b72828f1-1202-4689-85a9-4391096062b4" width="150"><br>PTX-YK1-QMIMB | no | yes | yes | no
| [Aqara Cube EU](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Cube-EU-(MFKZQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MFKZQ01LM.png" width="150"><br>MFKZQ01LM | yes | no | no | no

## <a name="installation">Installation</a>

via nuget package manager

```nuget
Install-Package MiHomeLib
```

or

```nuget
dotnet add package MiHomeLib
```

or install via [GitHub packages](https://github.com/sergey-brutsky/mi-home/pkgs/nuget/MiHomeLib)

<a name="setup-xiaomi-gateway-2"></a>
<a name="setup-xiaomi-multimode-gateway-same-for-multimode-gateway-2"></a>
<a name="setup-aqara-hub-e1-gateway"></a>

## <a name="setup-gateway"></a>Gateway setup

Use MiHomeSetup to check readiness and prepare a supported gateway for the library. Run it from the repository root with the .NET 10 SDK and GNU Make installed.

- **Xiaomi Gateway 2:** requires LAN access/development mode and UDP multicast connectivity on the gateway's local network.
- **Multimode Gateway, Multimode Gateway 2 and Aqara Hub E1 China:** require MQTT on port 1883 and passwordless administrative Telnet on port 23. Automatic activation depends on the firmware; a miIO token and, on some firmware, a separate saved gateway key are required.

For `make gw check/setup` commands, supported firmware, credentials, reboot behavior and troubleshooting, follow the **[gateway setup guide in the wiki](https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup)**. `check` is read-only; `setup` changes gateway access and may restart MQTT services.

## <a name="basic-scenario">Basic scenarios</a>

Subscribe during discovery and keep the gateway alive while listening. These console examples wait for Enter before disposing the gateway.

Xiaomi Gateway 2

```csharp
using System;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2;

public static class Gateway2Example
{
    public static async Task RunAsync(string ip, string token)
    {
        using var gateway = new XiaomiGateway2(ip, token);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            Console.WriteLine(device);
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        Console.ReadLine();
    }
}
```

Xiaomi Multimode Gateway

```csharp
using System;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway;

public static class MultimodeExample
{
    public static async Task RunAsync(string ip, string token, string did)
    {
        using var gateway = new MultimodeGateway(ip, token, did);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            Console.WriteLine(device);
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        Console.ReadLine();
    }
}
```

Xiaomi Multimode Gateway 2 Global

```csharp
using System;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway;

public static class Multimode2GlobalExample
{
    public static async Task RunAsync(string ip, string token, string did)
    {
        using var gateway = new MultimodeGateway2Global(ip, token, did);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            Console.WriteLine(device);
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        Console.ReadLine();
    }
}
```

Xiaomi Multimode Gateway 2 China

```csharp
using System;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway;

public static class Multimode2ChinaExample
{
    public static async Task RunAsync(string ip, string token, string did)
    {
        using var gateway = new MultimodeGateway2China(ip, token, did);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            Console.WriteLine(device);
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        Console.ReadLine();
    }
}
```

Aqara Hub E1 China

```csharp
using System;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway;

public static class HubE1Example
{
    public static async Task RunAsync(string ip, string token, string did)
    {
        using var gateway = new AqaraHubE1China(ip, token, did);
        gateway.OnDeviceDiscoveredAsync += device =>
        {
            Console.WriteLine(device);
            return Task.CompletedTask;
        };
        await gateway.DiscoverDevicesAsync();
        Console.ReadLine();
    }
}
```

## <a name="logging">Logging</a>

The library is silent by default. To see what happens inside, pass any `ILoggerFactory` to a gateway constructor. The gateway and all its devices log through it:

```csharp
using MiHomeLib.MqttGateway;
using Microsoft.Extensions.Logging;

public static class LoggingExample
{
    public static void Create(string ip, string token, string did)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        using var gateway = new MultimodeGateway(ip, token, did, 1883, loggerFactory);
        gateway.DiscoverDevices();
    }
}
```

Install `Microsoft.Extensions.Logging.Console` to use `AddConsole`. Set a process-wide default for subsequently created devices with `MiHomeLib.MiHomeLogging.SetLoggerFactory(loggerFactory)`.

Levels: `Debug` shows every message received from the gateway, `Information` discovery and connection events, `Warning` messages or devices the library does not support yet. Each device logs under its own type name, e.g. `MiHomeLib.Devices.XiaomiSmartWirelessSwitch`, so logging can be enabled for one device only.

## <a name="documentation">Documentation</a>

Check detailed documentation on how to work with different devices in the [project's WIKI](https://github.com/sergey-brutsky/mi-home/wiki)

## <a name="contribution">Contribution</a>

Your pull requests are welcome to replenish the database of supported devices
