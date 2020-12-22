using System;

namespace MiHomeLib.Events
{
    public class DiscoverEventArgs : EventArgs
    {
        public DiscoverEventArgs(string ip, string type, string serial, string token)
        {
            Ip = ip;
            Type = type;
            Serial = serial;
            Token = token;
        }

        public string Ip { get; }
        public string Type { get; }
        public string Serial { get; }
        public string Token { get; }
    }
}