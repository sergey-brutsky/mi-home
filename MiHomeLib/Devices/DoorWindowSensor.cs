﻿using System;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class DoorWindowSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    public const string TypeKey = "magnet";

    public event EventHandler OnOpen;

    public event EventHandler OnClose;

    public event EventHandler NotClosedFor1Minute;

    public event EventHandler NotClosedFor5Minutes;

    public float? Voltage { get; set; }

    public string Status { get; private set; }

    public override void ParseData(string command)
    {
        var jObject = JsonNode.Parse(command).AsObject();
        
        if (jObject.ParseString("status", out string status))
        {
            if (status == "open")
            {
                Status = status;
                OnOpen?.Invoke(this, EventArgs.Empty);
            }
            else if (status == "close")
            {
                Status = status;
                OnClose?.Invoke(this, EventArgs.Empty);
            }
        }

        if (jObject.ParseInt("no_close", out int noClose))
        {
            if(noClose == 60) NotClosedFor1Minute?.Invoke(this, EventArgs.Empty);
            else if(noClose == 300) NotClosedFor5Minutes?.Invoke(this, EventArgs.Empty);
        }

        Voltage = jObject.ParseVoltage();
    }
    public override string ToString()
    {
        return $"Status: {Status}, Voltage: {Voltage}V";
    }
}