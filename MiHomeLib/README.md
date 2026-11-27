## 3.3.1

- Fixed: a miIO request no longer fails when a corrupted or non-miIO datagram arrives before the device answer; such datagrams are skipped. When only answers with an invalid checksum arrive (usually a wrong token), the request still fails with the checksum error, now after the response timeout instead of immediately.
- Extended robustness tests: invalid values for every mapped value of every supported device, seeded random MQTT packets, and long or corrupted miIO datagrams.
- The 3.3.0 public API is recorded as shipped, and package validation now compares against 3.3.0. No public API changes.
- The `MiotProperty` generator is documented as a tool for new MIoT devices; existing devices keep their hand-written methods.
- Known limitation: malformed SQLite databases still throw `InvalidDataException`.

Supported targets: .NET Standard 2.0, .NET Framework 4.6.1 and 4.6.2, and .NET 10.

Gateway setup guide: https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup
Worker Service guide: https://github.com/sergey-brutsky/mi-home/wiki/Worker-Service