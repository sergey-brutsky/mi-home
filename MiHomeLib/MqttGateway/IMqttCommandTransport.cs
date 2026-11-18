using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway;

/// <summary>Optional transport capability for a Lumi write completed by a matching device write_ack.</summary>
internal interface IMqttCommandTransport
{
    Task SendCommandAsync(string message);
}
