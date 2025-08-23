# C# Library for using xiaomi smart gateway in your automation scenarious

[![Build project](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml/badge.svg)](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml)
[![Tests](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/sergey-brutsky/d70d7e06eb53484b7514bfd63cec6885/raw)](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml)
[![Nuget](https://img.shields.io/nuget/v/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![Nuget](https://img.shields.io/nuget/dt/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sergey-brutsky/mi-home/blob/master/LICENSE.md)

This library provides simple and flexible C# API for Xiaomi smart devices.  

Currently supports **only [Gateway version 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Gateway-2) (DGNWG02LM), [Multimode Gateway](https://github.com/sergey-brutsky/mi-home/wiki/Multimode-Gateway) (ZNDMWG03LM), [Multimode Gateway 2](https://github.com/sergey-brutsky/mi-home/wiki/Multimode-Gateway-2) (ZNDMWG04LM)**, [Air Humidifier](https://github.com/sergey-brutsky/mi-home/wiki/Air-Humidifier-(MJJSQ03DY)) (zhimi.humidifier.v1), [Mi Robot vacuum](https://github.com/sergey-brutsky/mi-home/wiki/Mi-Robot-Vacuum-(SDJQR02RR)) (rockrobo.vacuum.v1),
[Mi Robot Mop 3C](https://github.com/sergey-brutsky/mi-home/wiki/Mi-Robot-Mop3C-(B106CN)) (ijai.vacuum.v18) and several sensors. See table below.

![xiaomi-gateway-2](https://user-images.githubusercontent.com/5664637/118375593-46751980-b5cb-11eb-81f9-93b095401737.jpeg)

## Supported gateway devices/sensors
| Device support | Gateway 2 | Multimode Gateway | Multimode Gateway 2 |
|:---: |:---: |:---: |:---: |
| [Aqara Vibration Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Vibration-sensor-(DJT11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/DJT11LM.png" width="150"><br>DJT11LM | yes | yes | yes |
| [Xiaomi Door/Window Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Door-Window-sensor-(MCCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ01LM.png" width="150"><br>MCCGQ01LM | yes | yes | yes |
| [Xiaomi Door/Window Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Door-Window-sensor-2-(MCCGQ02HL))<br><img src="https://github.com/sergey-brutsky/mi-home/assets/5664637/4ac8671c-394d-4ef1-ba7a-be5f670ca103" width="150"><br>MCCGQ02HL | no | yes | yes |
| [Aqara Door/Window Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Door-Window-sensor-(MCCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ11LM.png" width="150"><br>MCCGQ11LM | yes | yes | yes |
| [Xiaomi TH Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-TH-sensor-(WSDCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ01LM.png" width="150"><br>WSDCGQ01LM | yes | yes | yes |
| [Xiaomi TH Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-TH-sensor-2-(LYWSD03MMC))<br><img src="https://www.zigbee2mqtt.io/images/devices/LYWSD03MMC.png" width="150"><br>LYWSD03MMC | no | yes | yes |
| [Aqara TH Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-TH-sensor-(WSDCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ11LM.png" width="150"><br>WSDCGQ11LM | yes | yes | yes |
| [Aqara Water Leak Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Water-Leak-sensor-(SJCGQ11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/SJCGQ11LM.png" width="150"><br>SJCGQ11LM | yes | yes | yes |
| [Xiaomi Motion Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Motion-sensor-(RTCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ01LM.png" width="150"><br>RTCGQ01LM | yes | yes | yes |
| [Xiaomi Motion Sensor 2](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Motion-sensor-2-(RTCGQ02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ12LM.png" width="150"><br>RTCGQ02LM | no | yes | yes |
| [Aqara Relay T1 EU (with N)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Relay-T1-EU-(SSM%E2%80%90U01))<br><img src="https://www.zigbee2mqtt.io/images/devices/SSM-U01.png" width="150"><br>SSM-U01 | no | yes | yes |
| [Aqara Relay CN](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Relay-CN-(LLKZMK11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/LLKZMK11LM.png" width="150"><br>LLKZMK11LM | no | yes | yes |
| [Aqara Opple Switch (2 buttons)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Opple-Switch-(2-buttons)-(WXCJKG11LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG11LM.png" width="150"><br>WXCJKG11LM | no | yes | yes |
| [Aqara Opple Switch (4 buttons)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Opple-Switch-(4-buttons)-(WXCJKG12LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG12LM.png" width="150"><br>WXCJKG12LM | no | yes | yes |
| [Honeywell Smoke Sensor](https://github.com/sergey-brutsky/mi-home/wiki/Honeywell-Smoke-Sensor-(JTYJ%E2%80%90GD%E2%80%9001LM-BW))<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-01LM/BW | yes | yes | yes |
| [Honeywell Smoke Alarm](https://github.com/sergey-brutsky/mi-home/wiki/Honeywell-Smoke-Alarm-(JTYJ%E2%80%90GD%E2%80%9003MI))<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-03MI | no | yes | yes |
| [Xiaomi Wireless Button](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Wireless-Button-(WXKG01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG01LM.png" width="150"><br>WXKG01LM | yes | yes | yes |
| [Xiaomi Plug CN](https://github.com/sergey-brutsky/mi-home/wiki/Xiaomi-Plug-CN-(ZNCZ02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/ZNCZ02LM.png" width="150"><br>ZNCZ02LM | yes | yes | yes |
| [Aqara Double Wall Switch (no N)](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Double-Wall-Switch-(no-N)-(QBKG03LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/QBKG03LM.png" width="150"><br>QBKG03LM | yes | no | no |
| [Aqara Double Wall Button CN](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Double-Wall-Button-CN-(WXKG02LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG02LM_rev2.png" width="150"><br>WXKG02LM | yes | no | no |
| [Aqara Cube EU](https://github.com/sergey-brutsky/mi-home/wiki/Aqara-Cube-EU-(MFKZQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MFKZQ01LM.png" width="150"><br>MFKZQ01LM | yes | no | no |

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

## <a name="setup-gateway">Setup Xiaomi Gateway 2</a>

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

## <a name="setup-gateway">Setup Xiaomi Multimode Gateway (same for Multimode Gateway 2)</a>

Before using this library:

1. Open telnet on your gateway
2. Expose MQTT broker to the world
3. Extract token to work with your gateway

The easisest way is to setup/configure [this HA integration](https://github.com/AlexxIT/XiaomiGateway3/) (it does all aforementioned things automatically).

The way of warrior:
1. [Enable telnet on your gateway](https://gist.github.com/zvldz/1bd6b21539f84339c218f9427e022709)
2. Download this [openmiio_agent](http://github.com/AlexxIT/openmiio_agent/releases/download/v1.2.1/openmiio_agent_mips) and upload it to your gateway (for example to /data/openmiio_agent) via telnet
3. Login to your gateway via telnet `telnet <gateway ip> 23` (login: admin or root, pwd: empty)
4. Kill embedded mosquitto mqtt broker and run openmiio_agent (it will expose mqtt port 1883 to the world) `kill -9 <pid of mosquitto> && /data/openmiio_agent mqtt &`
5. Check that mosquitto is binded to `0.0.0.0 1883` `netstat -ntlp | grep mosquitto`
6. [Extract token instructions](https://github.com/jghaanstra/com.xiaomi-miio/blob/master/docs/obtain_token.md)

## <a name="basic-scenario">Basic scenarios</a>
Get all devices in the network from the **Xiaomi Gateway 2**

```csharp
public static void Main(string[] args)
{
    // gateway password is optional, needed only to send commands to your devices
    // gateway sid is optional, use only when you have 2 gateways in your LAN
    // using var gw2 = new XiaomiGateway2("ip", "token", "gateway password", "gateway sid");
    using var gw2 = new XiaomiGateway2("<gateway ip>", "<gateway token>");
    {
        gw2.OnDeviceDiscoveredAsync += d =>
        {
            Console.WriteLine(d.ToString());
            return Task.CompletedTask;
        };

        gw2.DiscoverDevices();
    }
}
```

Get all devices in the network from the **Xiaomi Multimode Gateway**

```csharp
public static void Main(string[] args)
{
    using var multimodeGw = new MultimodeGateway("<gateway ip>", "<gateway token>");
    {
        multimodeGw.OnDeviceDiscoveredAsync += d =>
        {
            Console.WriteLine(d.ToString());
            return Task.CompletedTask;
        };

        multimodeGw.DiscoverDevices();
    }
}
```
Get all devices in the network from the **Xiaomi Multimode Gateway 2**

```csharp
public static void Main(string[] args)
{
    using var multimodeGw2 = new MultimodeGateway2Global("<gateway ip>", "<gateway token>");
    {
        multimodeGw2.OnDeviceDiscoveredAsync += d =>
        {
            Console.WriteLine(d.ToString());
            return Task.CompletedTask;
        };

        multimodeGw2.DiscoverDevices();
    }
}
```

## <a name="documentation">Documentation</a>
Check detailed documentation on how to work with different devices in the [project's WIKI](https://github.com/sergey-brutsky/mi-home/wiki)

## <a name="contribution">Contribution</a>
Your pull requests are welcome to replenish the database of supported devices