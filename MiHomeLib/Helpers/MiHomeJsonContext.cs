using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeLib.Helpers;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JsonElement[]))]
[JsonSerializable(typeof(List<MiHomeLib.XiaomiGateway2.XiaomiGateway2.RadioChannel>))]
[JsonSerializable(typeof(ZigBeeTransport.ZigbeeWriteCommand))]
[JsonSerializable(typeof(RemoteControlPayload))]
[JsonSerializable(typeof(RoomCleaningPayload))]
[JsonSerializable(typeof(SettingPayload))]
[JsonSerializable(typeof(UploadPayload))]
[JsonSerializable(typeof(MiotReadProperty))]
[JsonSerializable(typeof(MiotWriteProperty))]
[JsonSerializable(typeof(MiotAction))]
[JsonSerializable(typeof(MiotActionInput))]
[JsonSerializable(typeof(MeshCommand))]
[JsonSerializable(typeof(RadioStart))]
[JsonSerializable(typeof(RadioChannels))]
internal partial class MiHomeJsonContext : JsonSerializerContext { }

internal sealed record RemoteControlPayload(int Spdv, int Spdw, string Audio, int Random);
internal sealed record RoomCleaningPayload(List<int[]> Selects);
internal sealed record SettingPayload(string K, JsonNode V);
internal sealed record UploadPayload(string Id, string Url, string Md5, int Size);
internal sealed record MiotReadProperty(string Did, int Siid, int Piid);
internal sealed record MiotWriteProperty(string Did, int Siid, int Piid, JsonNode Value);
internal sealed record MiotAction(string Did, int Siid, int Aiid, [property: JsonPropertyName("in")] JsonNode In);
internal sealed record MiotActionInput(int Piid, JsonNode Value);
internal sealed record MeshCommand(string Method, MiotWriteProperty[] Params);
internal sealed record RadioStart(int Start);
internal sealed record RadioChannels(List<MiHomeLib.XiaomiGateway2.XiaomiGateway2.RadioChannel> Chs);
