using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MiHomeLib.Utils;

public static class NetworkHelpers
{
    public static void SendTo(this UdpClient udpClient, byte[] bytes, IPEndPoint endpoint)
    {
        udpClient.Send(bytes, bytes.Length, endpoint);
    }

    public static Task<int> SendAsync(this UdpClient udpClient, byte[] bytes, IPEndPoint endpoint)
    {
        return udpClient.SendAsync(bytes, bytes.Length, endpoint);
    }

    public static async Task<byte[]> ReceiveBytesAsync(this Socket socket, int bufferSize = 4096)
    {
        var buffer = new ArraySegment<byte>(new byte[bufferSize]);
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false);

        if (received > bufferSize)
        {
            throw new Exception("Data received is greater than buffer size");
        }

        return buffer.Take(received).ToArray();
    }

    public static byte[] ReceiveBytes(this Socket socket, int bufferSize = 4096)
    {
        var buffer = new byte[bufferSize];

        var recevied = socket.Receive(buffer);

        if (recevied > bufferSize)
        {
            throw new Exception("Data received is greater than buffer size");
        }

        return new ArraySegment<byte>(buffer, 0, recevied).ToArray();
    }

    public static byte[] ReceiveBytes(this NetworkStream stream, int bufferSize = 4096)
    {
        var buffer = new byte[bufferSize];

        var recevied = stream.Read(buffer, 0, buffer.Length);

        if (recevied > bufferSize)
        {
            throw new Exception("Data received is greater than buffer size");
        }

        return new ArraySegment<byte>(buffer, 0, recevied).ToArray();
    }

    public static void Write(this NetworkStream stream, string s)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(s);
        
        stream.Write(bytes, 0, bytes.Length);
    }
}
