- Added support for trimming and Native AOT in standard MiHomeLib scenarios on .NET 10. 
- Added XML documentation for IntelliSense and a SourceLink symbol package for debugging.
- Added correctly spelled Sensitivity APIs and Opple WirelessSwitch types. The original names remain available and are marked obsolete.
- Protected miIO helpers that serialize arbitrary objects now carry trimming and Native AOT warnings on .NET 10.
- Added hardware tests, recorded message replay, and CI checks on Linux, Windows, and macOS.

Gateway setup guide: https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup
When upgrading from 2.x, review the 3.0 breaking changes and migration guide:
Release notes: https://github.com/sergey-brutsky/mi-home/wiki/Release-Notes-3.0
Migration guide: https://github.com/sergey-brutsky/mi-home/wiki/Migration-3.0