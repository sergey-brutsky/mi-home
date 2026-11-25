# Hardware tests

This opt-in project is excluded from `MiHome.sln` and CI test workflows. Run it manually, separately from
`MiHomeUnitTests`; tests skip when their gateway is unconfigured.

## Configuration

Set the environment variables in the shell where you will run the tests. Replace the placeholders with your gateway details.

```sh
export MIHOME_GW2_IP="<IP_GATEWAY2>"
export MIHOME_GW2_TOKEN="<MIIO_TOKEN>"

export MIHOME_MQTT_KIND="MultimodeGateway"
export MIHOME_MQTT_IP="<IP_MQTT_GATEWAY>"
export MIHOME_MQTT_TOKEN="<MIIO_TOKEN>"
# Optional for MultimodeGateway, required for the other three kinds.
export MIHOME_MQTT_DID="<GATEWAY_DID>"
```

Set `MIHOME_MQTT_KIND` to `MultimodeGateway`, `MultimodeGateway2China`, `MultimodeGateway2Global`, or `AqaraHubE1China`.
You can configure either gateway or both. Each configured gateway is initialized once per test collection, runs device discovery,
and is disposed of after the tests. An unreachable configured gateway causes the tests to fail.

```sh
dotnet test MiHomeHardwareTests
dotnet test MiHomeHardwareTests --filter "Category!=Interactive"
dotnet test MiHomeHardwareTests --filter Category=Interactive --logger "console;verbosity=detailed"
```

The default hardware checks are read-only. Write tests and interactive checks require explicit opt-in. Device checks
skip when the required device is absent and treat unavailable BLE readings as unknown. They check device properties
and `ToString()`. The recording format tests can run without hardware.

## Write tests

Enable writes and list the device IDs the tests may control.

```sh
export MIHOME_ALLOW_WRITES=1
export MIHOME_WRITABLE="<DEVICE_ID_1>,<DEVICE_ID_2>"
```

The write tests cover the ZNCZ02LM plug, `AqaraDualWallSwitch`, `AqaraDualWirelessRelayCN`, `AqaraT1WithNeutral`,
and the mesh wall switch. Each test selects a device of the appropriate type from the allowlist, saves its original
state, switches it, and waits up to 10 seconds for its state property to confirm the change. In `finally`, the test
restores the original state and waits for confirmation.

Tests skip when the original state is unknown. The plug accepts a valid on/off value from discovery or a later report.
The mesh wall switch still requires an observed state report. Restoration is attempted even if the test fails. A lost connection
can prevent restoration, and the test reports that failure.

To run the Gateway 2 light test, set `MIHOME_ALLOW_WRITES=1` and include the gateway's own SID in `MIHOME_WRITABLE`.
The test turns the light white at 30% brightness and waits for an RGB report. It **always turns the light off** in
`finally`, regardless of its original state. If the expected report does not arrive, the error describes whether any RGB report was received.

## Interactive checks

```sh
export MIHOME_INTERACTIVE=1
dotnet test MiHomeHardwareTests --filter Category=Interactive --logger "console;verbosity=detailed"
```

The output prompts you to press a button, move in front of a sensor, or open or close a contact within 30 seconds.
Each test subscribes to the event before waiting and unsubscribes in `finally`. The test skips if the device is absent.

## Recording and replaying messages

```sh
export MIHOME_RECORD="/path/to/local-recordings"
dotnet test MiHomeHardwareTests --filter "Category!=Interactive"
```

The recording provider reads structured debug logs from the library and appends entries to `gateway2.jsonl` or `mqtt.jsonl`.
Recording runs for the duration of the tests. Use interactive tests to capture button presses.
Before saving valid JSON, the recorder removes `token` fields and masks configured miIO token values.
Review recordings before adding them to Git because messages may include device IDs.

Each line uses this format.

```json
{"gateway":"XiaomiGateway2","topic":null,"message":"{\"cmd\":\"read_ack\",...}"}
```

To add recordings to the regression tests, follow these steps.

1. Select the lines you want to add and check them for secrets.
2. Copy them to `MiHomeUnitTests/Fixtures/Recorded/gateway2.jsonl` or `mqtt.jsonl` in the same directory.
3. For MQTT recordings, add a **top-level** `"model"` field to each record, such as `"lumi.relay.c2acn01"`.
   Zigbee messages usually omit the model, so supply it when adding the recording.
4. Run `dotnet test MiHomeUnitTests --filter FullyQualifiedName~RecordedMessagesTests`.

CI replay tests check parsing of Gateway 2 and MQTT `report`, `heartbeat`, and `read_rsp` messages. The fixtures include
a recorded example of a nonzero `read_rsp` `error_code`, which the standard processor ignores. Adding files from
`MIHOME_RECORD` to the test fixtures is a manual step.
