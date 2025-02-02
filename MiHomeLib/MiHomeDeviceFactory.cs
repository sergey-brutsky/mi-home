using System;
using System.Collections.Generic;
using MiHomeLib.Contracts;
using MiHomeLib.Devices;

namespace MiHomeLib
{
    public class MiHomeDeviceFactory
    {
        private readonly IMessageTransport _transport;

        private readonly Dictionary<string, Func<string, MiHomeDevice>> _devicesMap;
        
        public MiHomeDeviceFactory(IMessageTransport transport)
        {
            _transport = transport;

            _devicesMap = new Dictionary<string, Func<string, MiHomeDevice>>
            {
                {AqaraVirationSensor.TypeKey, sid => new AqaraVirationSensor(sid)},
                {ThSensor.TypeKey, sid => new ThSensor(sid)},
                {WeatherSensor.TypeKey, sid => new WeatherSensor(sid)},
                {MotionSensor.TypeKey, sid => new MotionSensor(sid)},
                {SocketPlug.TypeKey, sid => new SocketPlug(sid, _transport)},
                {DoorWindowSensor.TypeKey, sid => new DoorWindowSensor(sid)},
                {WaterLeakSensor.TypeKey, sid => new WaterLeakSensor(sid)},
                {SmokeSensor.TypeKey, sid => new SmokeSensor(sid)},
                {Switch.TypeKey, sid => new Switch(sid)},
                {WiredDualWallSwitch.TypeKey, sid => new WiredDualWallSwitch(sid)},
                {WirelessDualWallSwitch.TypeKey, sid => new WirelessDualWallSwitch(sid)},
                {AqaraCubeSensor.TypeKey, sid => new AqaraCubeSensor(sid)},
                {AqaraMotionSensor.TypeKey, sid => new AqaraMotionSensor(sid)},
                {AqaraOpenCloseSensor.TypeKey, sid => new AqaraOpenCloseSensor(sid)}
            };
        }

        public MiHomeDevice CreateByModel(string model, string sid)
        {
            if(_devicesMap.ContainsKey(model))
            {
                return _devicesMap[model](sid);
            }
            else
            {
                throw new ModelNotSupportedException($"Device '{model}' currently is not supported" +
                    $"by this library, check https://github.com/sergey-brutsky/mi-home to get all supported devices");
            }
        }
    }
}
