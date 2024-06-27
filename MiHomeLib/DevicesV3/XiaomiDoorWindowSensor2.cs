using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public class XiaomiDoorWindowSensor2 : BleBatteryDevice
{
    public enum ContactState
    {
        Unknown = -1,
        Open = 0,
        Closed = 1,
        // To configure timeout, connect to the device via bluetooth and configure timeout in the
        // mobile application
        NotClosedAfterConfiguredTimeout = 2,
    }
    public enum LightState
    {
        Unknown = -1,
        NoLight = 0,
        LightDiscovered = 1,
    }
    public const string MARKET_MODEL = "MCCGQ02HL";
    public const string MODEL = "isa.magnet.dw2hl";
    public const int PDID = 2443;
    private const int CONTACT_EID = 4121;
    private const int LIGHT_EID = 4120;
    public XiaomiDoorWindowSensor2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(CONTACT_EID, x => 
        {
            var value = (ContactState)int.Parse(x);

            if(value == Contact) return; // prevent event duplication when state is actual

            var oldContact = Contact;            
            Contact = (ContactState)int.Parse(x);
            OnContactChange?.Invoke(oldContact);
        });

        EidToActions.Add(LIGHT_EID, x => 
        {
            var value = (LightState)int.Parse(x);

            if(value == Light) return; // prevent event duplication when state is actual

            var oldLight = Light;
            Light = (LightState)int.Parse(x);
            OnLightChange?.Invoke(oldLight);
        });
    }
    public ContactState Contact { get; set; } = ContactState.Unknown;  
    public event Action<ContactState> OnContactChange;
    public LightState Light { get; set; } = LightState.Unknown;
    public event Action<LightState> OnLightChange;
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Contact: {Contact}, " +
                $"Light: {Light}, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
