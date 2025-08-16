using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Transport;
using Moq;

namespace MiHomeUnitTests.XiaomiGateway2SubDevicesTests;

public class Gw2DeviceTests: MiioDeviceBase
{
    protected readonly Mock<IMessageTransport> _messageTransport;
    protected readonly NullLoggerFactory _loggerFactory;
    protected readonly Fixture _fixture = new();
    
    public Gw2DeviceTests()
    {
        _fixture.Customize<ILoggerFactory>(x => x.FromFactory(() => new NullLoggerFactory()));
        _messageTransport = new Mock<IMessageTransport>();
        _loggerFactory = new NullLoggerFactory();
    }

    public string CreateCommand(string cmd, string model, string sid, int short_id, Dictionary<string, object> data)
    {
        var dict = new Dictionary<string, object>()
        {
            { "cmd", cmd},
            { "model", model},
            { "sid", sid},
            { "short_id", short_id},
            { "data", JsonSerializer.Serialize(data) },
        };

        return JsonSerializer.Serialize(dict);
    }

    internal class Gw2Response
    {
        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public override string ToString()
        {
            return JsonSerializer.Serialize(Convert.ChangeType(this, GetType()), _options);
        }

        public string Cmd { get; set; }
        public string Sid { get; set; }
        public string Token { get; set; }
        public string Data { get; set; }
        public string Model { get; internal set; }
    }
}

