# C# Library for using xiaomi smart gateway in your automation scenarious

[![Build project](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml/badge.svg)](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml)
[![Tests](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/sergey-brutsky/d70d7e06eb53484b7514bfd63cec6885/raw)](https://github.com/sergey-brutsky/mi-home/actions/workflows/main.yml)
[![Nuget](https://buildstats.info/nuget/mihomelib)](https://www.nuget.org/packages/MiHomeLib)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sergey-brutsky/mi-home/blob/master/LICENSE.md)

This library provides simple and flexible C# API for Xiaomi smart devices.  

Currently supports **only Gateway version 2 (DGNWG02LM), Gateway version 3 (ZNDMWG03LM)**,  Air Humidifier (zhimi.humidifier.v1), Mi Robot vacuum (rockrobo.vacuum.v1) and several sensors. See table below.

![xiaomi-gateway-2](https://user-images.githubusercontent.com/5664637/118375593-46751980-b5cb-11eb-81f9-93b095401737.jpeg)

## Supported gateway devices/sensors
| Device| Gateway 2 support| Gateway 3 support |
|:---: |:---: |:---: |
| [Xiaomi Door/Window Sensor](./wiki/Xiaomi-Door-Window-sensor-(MCCGQ01LM))<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ01LM.png" width="150"><br>MCCGQ01LM | yes | yes |
| [Xiaomi Door/Window Sensor 2](#link-to-wiki-here)<br><img src="https://github.com/sergey-brutsky/mi-home/assets/5664637/4ac8671c-394d-4ef1-ba7a-be5f670ca103" width="150"><br>MCCGQ02HL | no | yes |
| [Aqara Door/Window Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/MCCGQ11LM.png" width="150"><br>MCCGQ11LM | yes | yes |
| [Xiaomi TH Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ01LM.png" width="150"><br>WSDCGQ01LM | yes | yes |
| [Xiaomi TH Sensor 2](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/LYWSD03MMC.png" width="150"><br>LYWSD03MMC | no | yes |
| [Aqara TH Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WSDCGQ11LM.png" width="150"><br>WSDCGQ11LM | yes | yes |
| [Aqara Water Leak Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/SJCGQ11LM.png" width="150"><br>SJCGQ11LM | yes | yes |
| [Xiaomi Motion Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ01LM.png" width="150"><br>RTCGQ01LM | yes | yes |
| [Xiaomi Motion Sensor 2](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/RTCGQ12LM.png" width="150"><br>RTCGQ02LM | no | yes |
| [Aqara Relay T1 EU (with N)](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/SSM-U01.png" width="150"><br>SSM-U01 | no | yes |
| [Aqara Relay CN](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/LLKZMK11LM.png" width="150"><br>LLKZMK11LM | no | yes |
| [Aqara Opple Switch (2 buttons)](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG11LM.png" width="150"><br>WXCJKG11LM | no | yes |
| [Aqara Opple Switch (4 buttons)](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WXCJKG12LM.png" width="150"><br>WXCJKG12LM | no | yes |
| [Honeywell Smoke Sensor](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-01LM/BW | yes | yes |
| [Honeywell Smoke Alarm](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/JTYJ-GD-01LM-BW.png" width="150"><br>JTYJ-GD-03MI | no | yes |
| [Xiaomi Wireless Button](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG01LM.png" width="150"><br>WXKG01LM | yes | yes |
| [Xiaomi Plug CN](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/ZNCZ02LM.png" width="150"><br>ZNCZ02LM | yes | yes |
| [Aqara Double Wall Switch (no N)](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/QBKG03LM.png" width="150"><br>QBKG03LM | yes | no |
| [Aqara Double Wall Button CN](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/WXKG02LM_rev2.png" width="150"><br>WXKG02LM | yes | no |
| [Aqara Cube EU](#link-to-wiki-here)<br><img src="https://www.zigbee2mqtt.io/images/devices/MFKZQ01LM.png" width="150"><br>MFKZQ01LM | yes | no |

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

## <a name="setup-gateway">Setup Xiaomi Gateway 3</a>

Before using this library:

1. Open telnet on your gateway
2. Expose MQTT broker to the world
3. Extract token to work with your gateway

The easisest way is to setup/configure [this HA integration](https://github.com/AlexxIT/XiaomiGateway3/) (it does all aforementioned things automatically).

The way of warrior:
1. [Enable telnet on your gateway](https://gist.github.com/zvldz/1bd6b21539f84339c218f9427e022709)
2. Download this [openmiio_agent](http://github.com/AlexxIT/openmiio_agent/releases/download/v1.2.1/openmiio_agent_mips) and upload it to your gateway (for example to /data/openmiio_agent) via telnet
3. Login to your gateway via telnet `telnet <gateway ip> 23` (login: admin, pwd: empty)
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
    // using var gw2 = new XiaomiGateway2("gateway password", "gateway sid");
    using var gw2 = new XiaomiGateway2();
   
    gw2.OnAnyDevice += (_, device) =>
    {
        Console.WriteLine($"{device.Sid}, {device.GetType()}, {device}"); // all discovered devices
    };

    Console.ReadLine();
}
```

Get all devices in the network from the **Xiaomi Gateway 3**

```csharp
public static void Main(string[] args)
{
    using var gw3 = new XiaomiGateway3("<gateway ip>", "<gateway token>");

    gw3.OnDeviceDiscovered += gw3SubDevice =>
    {
        Console.WriteLine(gw3SubDevice.ToString()); // all discovered devices
    };

    gw3.DiscoverDevices();

    Console.ReadLine();
}
```

## <a name="documentation">Documentation</a>
Check detailed documentation on how to work with different devices in the [project's WIKI](https://github.com/sergey-brutsky/mi-home/wiki)

## <a name="contribution">Contribution</a>
Your pull requests are welcome to replenish the database of supported devices