Supported targets: .NET Standard 2.0, .NET Framework 4.6.2, and .NET 10.

3.1.0: Added MiHomeSetup, a separate .NET 10 console tool in the repository for checking and preparing supported gateways, with make gw check/setup commands and automatic model detection. Added Gateway 2 LAN access setup and disable-lan, plus MQTT/Telnet preparation for supported Multimode Gateway, Multimode Gateway 2 and Aqara Hub E1 firmware. Improved miIO packet compatibility and gateway setup diagnostics. No breaking public API changes from 3.0.1.
Gateway setup guide: https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup

When upgrading from 2.x, review the 3.0 breaking changes and migration guide:
Release notes: https://github.com/sergey-brutsky/mi-home/wiki/Release-Notes-3.0
Migration guide: https://github.com/sergey-brutsky/mi-home/wiki/Migration-3.0
