# MiHomeSetup

For step-by-step gateway preparation with Make commands, see the [gateway setup guide in the project wiki](https://github.com/sergey-brutsky/mi-home/wiki/Gateway-Setup).

.NET 10 console application for checking and preparing Xiaomi Gateway 2 and Xiaomi/Aqara MQTT gateways.

```sh
dotnet run --project MiHomeSetup -- check --host 192.168.1.100 --model lumi.gateway.mgl03
dotnet run --project MiHomeSetup -- check --host 192.168.1.100
dotnet run --project MiHomeSetup -- check --host 192.168.1.100 --token-file ./gateway.token
dotnet run --project MiHomeSetup -- setup --host 192.168.1.100 --model lumi.gateway.mgl03 --token <32-character-miio-token>
dotnet run --project MiHomeSetup -- setup --host 192.168.1.100 --model lumi.gateway.mgl03 --token-file ./gateway.token
# For firmware requiring a separate saved gateway key
dotnet run --project MiHomeSetup -- setup --host 192.168.1.100 --model lumi.gateway.mgl03 --token-file ./gateway.token --key-file ./gateway.key
dotnet run --project MiHomeSetup -- download-agent --model lumi.gateway.mgl03 --output ./openmiio_agent_mips
```

The IP in these examples must be replaced with your gateway's address. Running `setup` permits installation and MQTT service restarts on that gateway. Connected MQTT clients will disconnect during preparation.

From the repository root, the Makefile provides equivalent commands. Pass optional arguments as make variables.

```sh
make build-gw-setup
make gw check 192.168.1.13 token="<GATEWAY2_MIIO_TOKEN>"
make gw setup 192.168.1.13 token="<GATEWAY2_MIIO_TOKEN>"
make gw check 192.168.1.100
make gw check 192.168.1.100 token="<MIIO_TOKEN>" key="<GATEWAY_KEY>"
make gw check 192.168.1.100 token_file='./gateway.token'
make gw check 192.168.1.100 model=lumi.gateway.mgl03
make gw setup 192.168.1.100 token="<MIIO_TOKEN>" key="<GATEWAY_KEY>"
make gw setup 192.168.1.100 model=lumi.gateway.mgl03 token_file='./gateway.token' key_file='./gateway.key'
# host= also supports IPv6 addresses
make gw check host=192.168.1.100 model=lumi.gateway.mgl03
```

Use `token_file` for `--token-file`, `key` for `--key`, and `key_file` for `--key-file`. Both check and setup accept either an inline key or a key file. These key options are mutually exclusive and require a token option. Quote file paths containing spaces. Run `make gw` as a separate invocation, with `gw` as the first target.

Gateway 2 (`lumi.gateway.v3`, DGNWG02LM) uses native diagnostics. `check` accepts an explicit model or detects it through authenticated miIO. Supply `token` or `token_file`. The tool verifies miIO model information, derives the expected gateway SID from its MAC, and runs the library's existing UDP discovery. This prevents an acknowledgement from another multicast gateway from passing the check. Output reports the number of supported sub-devices found. Discovery can complete with zero supported devices, and devices that do not respond can remain absent.

Gateway 2 diagnostics read the current developer key through miIO and report its availability without displaying it. An unavailable key produces a warning and setup instructions. A READY result covers authenticated miIO and UDP discovery. Sub-device write behavior requires separate verification.

For Gateway 2, `setup` enables LAN access using `set_lumi_dpf_aes_key`. It reuses a current 16-character developer key or generates a cryptographically random 16-character key when the current key is empty. An optional `key`/`key_file` (`--key`/`--key-file`) explicitly selects a replacement developer key. After writing, setup verifies the key through miIO and checks UDP discovery, retrying discovery while LAN access starts. Keys are never printed or saved locally. The library can retrieve the developer key from the hub through miIO. A lost write acknowledgement is checked through readback rather than repeating the write.

To disable LAN access manually for testing, clear the developer key with the Gateway 2-only command:

```sh
make gw disable-lan 192.168.1.13 token="<MIIO_TOKEN>"
# Equivalent direct invocation:
dotnet run --project MiHomeSetup -- disable-lan --host 192.168.1.13 --token "<MIIO_TOKEN>"
# Enable it again, detecting the model automatically:
make gw setup 192.168.1.13 token="<MIIO_TOKEN>"
```

Gateway 2 preparation uses small request IDs (1–9998), JSON separators matching python-miio, and NUL-terminated encrypted payloads. It verifies `miIO.info` on the preparation connection before the key command and continues to require a matching response ID. The LAN access disable/setup sequence has been physically verified on Gateway 2 firmware 1.4.1_176 (MW300).

`disable-lan` sends `set_lumi_dpf_aes_key` with parameters `[""]` and verifies that the stored key is empty, reading up to five times to allow for delayed updates. If the firmware acknowledges the command but keeps a nonempty key, it reports failure with the returned string length (without revealing the value); check the LAN access switch in Mi Home. This disables LAN access used by existing UDP clients. Both commands also accept `token_file`. `check` remains read-only. Gateway 2 uses multicast `224.0.0.50` and UDP port `9898`; it needs no Telnet access or agent installation. The `download-agent` command applies to MQTT gateways.

Supported MQTT profiles are `lumi.gateway.mgl03` (Multimode Gateway), `lumi.gateway.mcn001` (Multimode Gateway 2 CN), `lumi.gateway.mgl001` (Multimode Gateway 2 Global), and `lumi.gateway.aqcn02` (Aqara E1 CN). `download-agent` requires an explicit model. Both `check` and `setup` can detect the model through miIO or existing Telnet access, and accept an explicit model override. `setup` verifies the actual model and CPU architecture before installation and prints firmware information. These profiles need physical validation on their respective firmware versions.

For `check` and `setup`, `--model` is optional. With `--token` or `--token-file`, detection first reads the `model` field from the authenticated `miIO.info` request. Without a token, or if miIO cannot return model information, detection uses existing Telnet access. It recognizes the gateway family from its login banner, logs in, and reads only the model from the gateway configuration. Reading the actual model distinguishes CN and Global Gateway 2 variants that share a banner. Unknown banners, unsupported models, and ambiguous configuration results cause an error asking for an explicit model or valid miIO credentials. Detection uses the selected host only and preserves the gateway configuration.

The miIO discovery/hello response exposes a device ID and timestamp, without a string model. `miIO.info` requires a token. When both Telnet and authenticated miIO are unavailable, automatic model detection is unavailable. An explicit `--model` skips detection and allows the readiness checks to report individual MQTT/discovery failures.

`setup` first checks TCP port 23. When Telnet is already accessible, it proceeds to administrative login and the existing preparation flow. When the port is closed, supply the 32-character hexadecimal miIO token using `--token <value>` or `--token-file <path>` to allow activation through miIO on UDP port 54321. These options are mutually exclusive. An optional `--key <value>` or `--key-file <path>` works with either token option and supplies the separate 16-character gateway key saved from XiaomiGateway3. Files may have a trailing newline. Credentials are read when activation is needed and are kept in memory. They are not printed or saved by MiHomeSetup.

Before activation the tool reads `miIO.info`, checks that the actual model matches `--model`, and selects one method from the following firmware rules.

| Model | Firmware family | Activation method |
|---|---|---|
| Multimode Gateway | `1.4.6`, build below `0043` | `enable_telnet_service` |
| Multimode Gateway | `1.4.6`, build `0043` and later; `1.4.7`; `1.5.0` through `1.5.4` | `set_ip_info` |
| Multimode Gateway | `1.5.5` through `1.5.7` | `system_command`, requires gateway key |
| Multimode Gateway 2 CN/Global | `1.0.3` through `1.0.6` | `set_ip_info` |
| Multimode Gateway 2 CN/Global | `1.0.7` through `1.0.8` | `system_command`, requires gateway key |
| Aqara E1 CN | `4.0.1` | `set_ip_info` |
| Aqara E1 CN | `4.0.4` | `system_command`, requires gateway key |

Firmware is parsed into numeric major, minor, patch and optional build components. Build numbers within these families use the same rule except for the explicit `1.4.6_0043` boundary. Firmware `1.4.6` without a build is ambiguous and requires manual activation. Unknown versions, missing keys, and model mismatches stop automatic activation. These rules follow the upstream implementation and documented firmware families, with conservative bounds. Firmware `1.4.6` and `1.4.7` are legacy versions described as unsupported upstream. Physical validation is pending.

The `set_ip_info` activation path uses a firmware command-injection vulnerability. The `system_command` path authenticates with a password derived from the miIO device ID, MAC and supplied gateway key. The preparation command removes the administrative password and starts Telnet, exposing administrative access to the LAN. Activation is sent once. A lost response triggers port checks, and the next stage verifies administrative login. An acknowledgement alone does not establish readiness. Password-protected Telnet remains unsupported by the library discovery implementation.

For firmware requiring a key, supply your previously saved key or enable Telnet manually. Automatic key retrieval through diagnostic logs is outside this implementation. The upstream activation logic and authentication formula are documented in [XiaomiGateway3 core utilities](https://github.com/AlexxIT/XiaomiGateway3/blob/master/custom_components/xiaomi_gateway3/core/core_utils.py).

When a key is supplied to `check`, its format (and file readability for `--key-file`) is checked. Authenticating the key requires the activation request performed by `setup`. Check explains that setup will verify a supplied key and redacts inline token and key values in suggested commands.

`check` verifies MQTT CONNECT/CONNACK and subscription acknowledgements on port 1883 using the library transport. It reads and parses the same Zigbee and BLE/Mesh discovery files as MiHomeLib. E1 has no BLE/Mesh check. Device control commands are not published. A NOT READY result includes concise service failures and a next action. With miIO firmware information, check uses the same compatibility rules as setup to explain whether activation is supported and whether a separate key is required. Unsupported firmware and discovery-file errors receive manual instructions. Commands in the advice use a token placeholder or the supplied token-file path. The token value is not echoed. A failed readiness check returns exit code 1, which make reports as `Error 1`. A READY result covers MQTT and initial discovery. Token validity, miIO commands, future reports, and operation after reboot require separate verification. Empty device lists are valid.

After administrative access is established, `setup` checks discovery and prepares the agent according to the XiaomiGateway3 integration conventions. The executable is `/data/openmiio_agent`, using the pinned v1.2.1 release and the same MIPS/ARM fingerprints. Its startup arguments are `miio mqtt cache central z3 --zigbee.tcp=8888`, and its log is `/var/log/openmiio.log`. These modules match the integration's miIO forwarding, MQTT, cache, BLE/Mesh and Zigbee features, including the Zigbee TCP endpoint on port 8888.

A matching executable and an already running process with the expected path and options are reused when readiness checks pass. Setup skips downloading, stopping and restarting a ready compatible agent. A stale executable, an agent previously started from `/tmp`, or one running only in `mqtt` mode is brought to the compatible path and startup options. Discovery file errors must be resolved before installation. Uploads are verified before replacement, and an existing executable is retained under a unique backup name. The agent performs firmware-specific broker preparation, including the patched `/tmp/mosquitto` copy on newer firmware.

When XiaomiGateway3 subsequently prepares the gateway, its current implementation checks the executable fingerprint at `/data/openmiio_agent` and whether an `openmiio_agent` process is running. Our installation is intended to satisfy these checks. Future integration releases may change the expected version and perform their normal agent update. Compatibility with sequential MiHomeSetup and Home Assistant startup still requires physical validation. Source conventions are recorded in [XiaomiGateway3 constants](https://github.com/AlexxIT/XiaomiGateway3/blob/master/custom_components/xiaomi_gateway3/core/shell/const.py) and [agent preparation](https://github.com/AlexxIT/XiaomiGateway3/blob/master/custom_components/xiaomi_gateway3/core/gate/openmiio.py).

On failure or cancellation after stopping services, setup attempts to restore the previous executable and restart it with the compatible startup options. If no previous executable existed, it attempts to start the embedded Mosquitto with `mosquitto -d`. Recovery is best effort and cannot guarantee restoration of custom process arguments or configuration. A broken Telnet connection can prevent recovery. Backup paths and recovery errors are printed for manual inspection.

A lock directory at `/tmp/mihome-setup.lock` prevents concurrent MiHomeSetup installations. An interrupted connection may leave this directory behind. Confirm that no setup is running before removing a stale lock. Other applications, including Home Assistant, do not observe this lock. Perform setup before enabling Home Assistant's gateway integration. Re-running setup can reuse an existing healthy compatible agent.

The agent executable and its backups remain in `/data`. Agent startup after reboot is handled by the application preparing the gateway, such as the XiaomiGateway3 integration. MiHomeSetup creates no boot script entry. Firmware behavior and previously installed custom startup entries can affect the post-reboot state. Enabling Telnet can change the administrative password even if later preparation fails, and MQTT recovery does not undo that activation. MiHome operation and reboot behavior require physical validation on the target firmware.

`download-agent` saves the selected MIPS or ARM binary locally and preserves an existing output file. Both download and setup use the fingerprints pinned in the upstream integration. MD5 is the upstream compatibility fingerprint, not an independent signature. The release and fingerprints come from [XiaomiGateway3 constants](https://github.com/AlexxIT/XiaomiGateway3/blob/master/custom_components/xiaomi_gateway3/core/shell/const.py). Its shell implementations provide the model-specific administrative conventions used here.

Exit codes are 0 for success/ready, 1 for failure/not ready, 2 for invalid arguments, and 130 for cancellation or the overall timeout. Ctrl+C cancels discovery, uploads and downloads. MQTT connection operations use the library's bounded timeouts. The overall operation timeout is 15 minutes, with additional bounded cleanup and recovery time.
