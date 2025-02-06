using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;
using MiHomeLib.Utils;

namespace MiHomeLib.DevicesV3;

public class AqaraVirationSensor : ZigBeeManageableBatteryDevice
{
    public enum AqaraVirationSensorState
    {
        Vibration = 1,
        Tilt = 2,
        FreeFall = 3,
    }
    public enum SensivityState
    {
        Low = 21,
        Middle = 11,
        High = 1,
    }
    public const string MARKET_MODEL = "DJT11LM";
    public const string MODEL = "lumi.vibration.aq1";
    private const string STATUS_RES_NAME = "13.1.85"; 
    private const string SENSIVITY_RES_NAME = "14.1.85"; 
    private const string FINAL_TILT_RES_NAME = "0.2.85";
    private const string BED_ACTIVITY_RES_NAME = "0.1.85";    
    public event Action OnVibration;
    public event Action OnTilt;
    public event Action OnFreeFall;
    public int FinalTiltAngle { get; internal set; }
    /// <summary>
    /// Old value from 0 to 180 passed as an argument
    /// </summary>
    public event Action<int> OnFinalTiltAngle;
    public int BedActivity { get; internal set; }
    /// <summary>
    /// Old value 0-255 passed as an argument, sorry don't know how to interpret this number :(
    /// All I found is this post --> https://github.com/Danielhiversen/PyXiaomiGateway/issues/86#issuecomment-418135852
    /// </summary>
    public event Action<int> OnBedActivity;
    private readonly IEnumerable<int> _validStateValues = Helpers.EnumToIntegers<AqaraVirationSensorState>();
    private readonly Dictionary<AqaraVirationSensorState, Action> _stateActions;
    public AqaraVirationSensor(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : base(did, mqttTransport, loggerFactory)
    {
        _stateActions = new Dictionary<AqaraVirationSensorState, Action>
        {
            {AqaraVirationSensorState.Vibration, () => OnVibration?.Invoke()},
            {AqaraVirationSensorState.Tilt, () => OnTilt?.Invoke()},
            {AqaraVirationSensorState.FreeFall, () => OnFreeFall?.Invoke()},
        };

        ResNamesToActions.Add(STATUS_RES_NAME, x =>
        {
            if(!_validStateValues.Contains(x.GetByte())) return;            
            _stateActions[(AqaraVirationSensorState)x.GetByte()]();
        });

        ResNamesToActions.Add(FINAL_TILT_RES_NAME, x =>
        {
            (var oldVal, FinalTiltAngle) = (FinalTiltAngle, x.GetInt32());
            OnFinalTiltAngle?.Invoke(oldVal);
        });

        ResNamesToActions.Add(BED_ACTIVITY_RES_NAME, x =>
        {
            (var oldVal, BedActivity) = (BedActivity, x.GetInt32());
            OnBedActivity?.Invoke(oldVal);
        });
    }
    public void SetSensivity(SensivityState state) => SendWriteCommand(SENSIVITY_RES_NAME, (int)state);
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
    }
}
