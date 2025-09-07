using System.Globalization;
using Microsoft.Extensions.Logging;
using MiHomeLib.Contracts;
using MiHomeLib.XiaomiGateway2.Commands;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class XiaomiMiSmartPowerPlugCN : ManageableXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "ZNCZ02LM";
    public const string MODEL = "plug";
    public XiaomiMiSmartPowerPlugCN(string sid, int shortId, IMessageTransport transport, string gwPassword, ILoggerFactory loggerFactory) : base(sid, shortId, transport, gwPassword, loggerFactory)
    {
        Actions.Add("status", x => 
        {
            Status = x.GetString();
        });

        Actions.Add("inuse", x => 
        {
            Inuse = int.Parse(x.GetString());
        });

        Actions.Add("power_consumed", x => 
        {
            PowerConsumed = int.Parse(x.GetString());
        });

        Actions.Add("load_power", x => 
        {
            LoadPower = float.Parse(x.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        });

        Actions.Add("voltage", x => {
            Voltage = x.GetInt32()/1000f;
        });
    }

    public float Voltage { get; private set; }
    public string Status { get; private set; }
    public int Inuse { get; private set; }
    public int PowerConsumed { get; private set; }
    public float LoadPower { get; private set; }
    public void TurnOff() => _transport.SendWriteCommand(Sid, MODEL, _gwPassword, new SocketPlugCommand("off"));
    public void TurnOn() => _transport.SendWriteCommand(Sid, MODEL, _gwPassword, new SocketPlugCommand("on"));
    public override string ToString()
    {
        return base.ToString() + $", Status: {Status}, Inuse: {Inuse}, Load Power: {LoadPower}V, Power Consumed: {PowerConsumed}W, Voltage: {Voltage}V";
    }
}