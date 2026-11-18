using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.XiaomiGateway2;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using System;
using FluentAssertions;
using MiHomeLib;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class EncodingAndEncryptionTests
{
    [Theory]
    [InlineData(null, "0123456789abcdef", "token")]
    [InlineData("0123456789abcdef", null, "gwPassword")]
    public void Missing_Gateway_Credentials_Are_Rejected(string token, string password, string parameter)
    {
        Action build = () => Gateway2KeyBuilder.BuildKey(token, password);
        build.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be(parameter);
    }

    [Theory]
    [InlineData(null, "0123456789abcdef", "ip")]
    [InlineData("127.0.0.1", null, "token")]
    public void Missing_Miio_Arguments_Identify_The_Parameter(string ip, string token, string parameter)
    {
        Action create = () => new MiioTransport(ip, token);
        create.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be(parameter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("00aBFF1020")]
    public void Hex_Roundtrip(string hex) => hex.ToByteArray().ToHex().Should().Be(hex.ToLowerInvariant());

    [Theory]
    [InlineData("0")]
    [InlineData("123")]
    [InlineData("zz")]
    [InlineData("xy00")]
    public void Invalid_Hex_Is_Rejected(string hex)
    {
        Action decode = () => hex.ToByteArray();
        decode.Should().Throw<FormatException>();
    }

    [Fact]
    public void Aes_Cbc_Matches_Known_First_Block_And_Roundtrips()
    {
        var iv = "000102030405060708090a0b0c0d0e0f".ToByteArray();
        var key = "2b7e151628aed2a6abf7158809cf4f3c".ToByteArray();
        var plaintext = "6bc1bee22e409f96e93d7e117393172a".ToByteArray();
        var encrypted = AesEncryption.EncryptData(iv, key, plaintext);
        encrypted.ToHex().Should().StartWith("7649abac8119b246cee98e9b12e9197d");
        AesEncryption.DecryptData(iv, key, encrypted).Should().Equal(plaintext);
    }

    [Fact]
    public void Gateway_Key_Is_Deterministic_And_One_Block()
    {
        var result = Gateway2KeyBuilder.BuildKey("0123456789abcdef", "0123456789abcdef");
        result.Should().HaveCount(16);
        Gateway2KeyBuilder.BuildKey("0123456789abcdef", "0123456789abcdef").Should().Equal(result);
    }

    [Fact]
    public void Unix_Time_Preserves_Local_Time_And_Milliseconds()
    {
        1700000000.125.UnixSecondsToDateTime().Should().Be(1700000000125d.UnixMilliSecondsToDateTime());
        0d.UnixSecondsToDateTime().Should().Be(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime());
    }
}
