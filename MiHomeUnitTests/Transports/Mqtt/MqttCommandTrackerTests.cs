using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using Xunit;

namespace MiHomeUnitTests.Transports.Mqtt;

public class MqttCommandTrackerTests
{
    [Fact]
    public async Task Parallel_Relay_Channels_Require_Their_Own_Id_And_Resource()
    {
        var tracker = new MqttCommandTracker();
        var firstId = 0;
        var secondId = 0;
        var first = tracker.SendAsync(Request, message => firstId = JsonNode.Parse(message)["id"].GetValue<int>(),
            TimeSpan.FromSeconds(2), CancellationToken.None);
        var second = tracker.SendAsync(Request.Replace("4.1.85", "4.2.85"), message => secondId = JsonNode.Parse(message)["id"].GetValue<int>(),
            TimeSpan.FromSeconds(2), CancellationToken.None);
        void Ack(int id, string resource) => tracker.Receive($$"""{"cmd":"write_ack","id":{{id}},"did":"plug","params":[{"res_name":"{{resource}}","value":1}]}""");
        Ack(firstId, "4.2.85");
        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        Ack(secondId, "4.2.85");
        await second;
        first.IsCompleted.Should().BeFalse();
        Ack(firstId, "4.1.85");
        await first;
    }

    private const string Request = """{"cmd":"write","did":"plug","params":[{"res_name":"4.1.85","value":1}]}""";

    [Fact]
    public async Task Only_Matching_Device_Acknowledgement_Completes_Command()
    {
        var tracker = new MqttCommandTracker();
        var id = 0;
        var command = tracker.SendAsync(Request, message => id = JsonNode.Parse(message)["id"].GetValue<int>(),
            TimeSpan.FromSeconds(2), CancellationToken.None);
        void Receive(string action, int responseId, string did, int value, int error = 0) =>
            tracker.Receive($$"""{"cmd":"{{action}}","id":{{responseId}},"did":"{{did}}","params":[{"res_name":"4.1.85","value":{{value}},"error_code":{{error}}}],"results":[{"res_name":"4.1.85","value":{{value}},"error_code":{{error}}}]}""");
        Receive("write_rsp", id, "plug", 1);
        Receive("write_ack", id + 1, "plug", 1);
        Receive("write_ack", id, "other", 1);
        Receive("write_ack", id, "plug", 0);
        command.IsCompleted.Should().BeFalse();
        Receive("write_ack", id, "plug", 1);
        await command;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejection_And_Disconnect_Fail_Pending_Command(bool disconnect)
    {
        var tracker = new MqttCommandTracker();
        var id = 0;
        var command = tracker.SendAsync(Request, message => id = JsonNode.Parse(message)["id"].GetValue<int>(),
            TimeSpan.FromSeconds(2), CancellationToken.None);
        if (disconnect) tracker.Disconnect();
        else tracker.Receive($$"""{"cmd":"write_rsp","id":{{id}},"did":"plug","results":[{"res_name":"4.1.85","error_code":-4003}]}""");
        Func<Task> finish = () => command;
        if (disconnect) await finish.Should().ThrowAsync<IOException>().WithMessage("*unknown*");
        else await finish.Should().ThrowAsync<InvalidOperationException>().WithMessage("*-4003*");
    }

    [Fact]
    public async Task Timeout_Does_Not_Repeat_Command_Or_Complete_Next_Command_With_Late_Ack()
    {
        var tracker = new MqttCommandTracker();
        var sent = 0;
        var oldId = 0;
        Func<Task> first = () => tracker.SendAsync(Request, message => { sent++; oldId = JsonNode.Parse(message)["id"].GetValue<int>(); },
            TimeSpan.FromMilliseconds(30), CancellationToken.None);
        await first.Should().ThrowAsync<TimeoutException>().WithMessage("*unknown*");
        var newId = 0;
        var next = tracker.SendAsync(Request, message => { sent++; newId = JsonNode.Parse(message)["id"].GetValue<int>(); },
            TimeSpan.FromSeconds(2), CancellationToken.None);
        tracker.Receive($$"""{"cmd":"write_ack","id":{{oldId}},"did":"plug","params":[{"res_name":"4.1.85","value":1}]}""");
        next.IsCompleted.Should().BeFalse();
        tracker.Receive($$"""{"cmd":"write_ack","id":{{newId}},"did":"plug","params":[{"res_name":"4.1.85","value":1}]}""");
        await next;
        sent.Should().Be(2);
    }
}
