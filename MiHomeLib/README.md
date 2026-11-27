## 3.3.0

- Added miIO request duration/retry, MQTT connection, and gateway message metrics, plus `MiHomeDiagnostics.MeterName`.
- Added a unified `DeviceEvent` stream on both gateway families and device/gateway availability tracking; Zigbee timeout defaults to 3 hours, and Gateway 2 availability is unknown until its first message.
- Added capability interfaces for temperature, humidity, pressure, battery, link quality, power metering, contact, smoke, and switching; dual-channel controls and motion behavior remain device-specific.
- Added cancellation overloads for gateway discovery, miIO device-id lookup, and miIO network discovery; other device operations are unchanged.
- Added independent loopback emulators, recorded-scenario playback, real-socket integration tests, and seeded parser/network fuzz tests (2,000 iterations by default; `MIHOME_FUZZ_ITERATIONS` overrides this).
- MQTT checks declared packet size before allocating the body; malformed or oversized packets trigger reconnect, after which valid messages are received.
- Added a Worker Service sample for gateway registration, device events, and graceful shutdown.
- Known limitations: invalid miIO response length/checksum can fail the active request; malformed SQLite databases still throw `InvalidDataException`.

Supported targets: .NET Standard 2.0, .NET Framework 4.6.1 and 4.6.2, and .NET 10.

Gateway setup guide: https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup
Worker Service guide: https://github.com/sergey-brutsky/mi-home/wiki/Worker-Service