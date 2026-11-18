using System;

namespace MiHomeLib.MqttGateway;

internal interface IMqttConnectionEvents
{
    event Action ConnectionLost;
    event Action ConnectionRestored;
}
