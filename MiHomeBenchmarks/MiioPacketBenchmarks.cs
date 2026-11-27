using BenchmarkDotNet.Attributes;
using MiHomeLib.MiioDevices.Internal;
using System.Text;
using System.Text.Json;

namespace MiHomeBenchmarks;

[MemoryDiagnoser]
public class MiioPacketBenchmarks
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private string _json;
    private MiioCipher _cipher;
    private MiioTransport.MiioPacket _headerPacket;
    private MiioTransport.MiioPacket _encryptedPacket;

    [GlobalSetup]
    public void Setup()
    {
        var header = new byte[32];
        header[0] = 0x21;
        header[1] = 0x31;
        header[3] = 32;
        header[11] = 1;
        Convert.FromHexString(Token).CopyTo(header, 16);
        _cipher = new MiioCipher(Token);
        const string prefix = "{\"id\":100001,\"method\":\"get_device_prop\",\"params\":[],\"padding\":\"";
        const string suffix = "\"}";
        _json = prefix + new string('x', 200 - Encoding.UTF8.GetByteCount(prefix) - Encoding.UTF8.GetByteCount(suffix)) + suffix;
        using var document = JsonDocument.Parse(_json);
        _headerPacket = new MiioTransport.MiioPacket(header);
        _encryptedPacket = new MiioTransport.MiioPacket(
            _headerPacket.BuildMessage(_json, _cipher, _headerPacket.Stamp));
    }

    [Benchmark]
    public byte[] EncryptJson() => _headerPacket.BuildMessage(_json, _cipher, _headerPacket.Stamp);

    [Benchmark]
    public string DecryptJson() => _encryptedPacket.GetResponseData(_cipher);
}
