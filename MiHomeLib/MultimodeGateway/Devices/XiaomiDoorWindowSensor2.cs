using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

public class XiaomiDoorWindowSensor2 : BleBatteryDevice
{
    public const string MARKET_MODEL = "MCCGQ02HL";
    public const string MODEL = "isa.magnet.dw2hl";
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
    public const int PDID = 2443;
    private const int CONTACT_EID = 4121;
    private const int LIGHT_EID = 4120;
    public XiaomiDoorWindowSensor2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(CONTACT_EID, async x => 
        {
            var value = (ContactState)int.Parse(x);

            if(value == Contact) return; // prevent event duplication when state is actual

            var oldContact = Contact;            
            Contact = (ContactState)int.Parse(x);
            await OnContactChangeAsync(oldContact);
        });

        EidToActions.Add(LIGHT_EID, async x => 
        {
            var value = (LightState)int.Parse(x);

            if(value == Light) return; // prevent event duplication when state is actual

            var oldLight = Light;
            Light = (LightState)int.Parse(x);
            await OnLightChangeAsync(oldLight);
        });
    }
    public ContactState Contact { get; set; } = ContactState.Unknown;  
    public event Func<ContactState, Task> OnContactChangeAsync = (_) => Task.CompletedTask;
    public LightState Light { get; set; } = LightState.Unknown;
    public event Func<LightState, Task> OnLightChangeAsync = (_) => Task.CompletedTask;
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Contact: {Contact}, " +
                $"Light: {Light}, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
