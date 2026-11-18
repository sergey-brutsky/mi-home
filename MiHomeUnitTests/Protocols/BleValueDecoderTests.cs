using System;
using FluentAssertions;
using MiHomeLib.Protocols;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class BleValueDecoderTests
{
    [Theory]
    [InlineData("00", 1, 0u)]
    [InlineData("fF", 1, 255u)]
    [InlineData("3412", 2, 0x1234u)]
    [InlineData("563412", 3, 0x123456u)]
    [InlineData("78563412", 4, 0x12345678u)]
    [InlineData("FFFFFFFF", 4, uint.MaxValue)]
    public void Unsigned_Values_Use_Little_Endian(string hex, int byteCount, uint expected) =>
        BleValueDecoder.DecodeUnsignedLittleEndian(hex, byteCount).Should().Be(expected);

    [Theory]
    [InlineData("00", 0)]
    [InlineData("aF", 175)]
    [InlineData("FF", 255)]
    public void Byte_Values_Accept_Both_Hex_Cases(string hex, byte expected) =>
        BleValueDecoder.DecodeByte(hex, "a battery value").Should().Be(expected);

    [Theory]
    [InlineData("0000", 0f)]
    [InlineData("fa00", 25f)]
    [InlineData("06ff", -25f)]
    [InlineData("ff7f", 3276.7f)]
    [InlineData("0080", -3276.8f)]
    [InlineData("FFFF", -0.1f)]
    public void Signed_Tenths_Preserve_The_Sign_And_Scale(string hex, float expected) =>
        BleValueDecoder.DecodeSignedTenths(hex).Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Invalid_Byte_Count_Is_Rejected_Before_Reading_The_Input(int byteCount)
    {
        Action decode = () => BleValueDecoder.DecodeUnsignedLittleEndian(null, byteCount);
        decode.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("byteCount");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("0000000000")]
    [InlineData("zz")]
    [InlineData("0g")]
    [InlineData("g0")]
    [InlineData(" 0")]
    public void Malformed_Hex_Is_Rejected_By_Each_Decoder(string hex)
    {
        Action[] decoders =
        [
            () => BleValueDecoder.DecodeByte(hex, "a battery value"),
            () => BleValueDecoder.DecodeSignedTenths(hex),
            () => BleValueDecoder.DecodeUnsignedLittleEndian(hex, 1)
        ];
        foreach (var decode in decoders)
        {
            if (hex is null)
                decode.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("hex");
            else
                decode.Should().ThrowExactly<FormatException>();
        }
    }

    [Theory]
    [InlineData("00gg")]
    [InlineData("00g0")]
    [InlineData("0000gg")]
    [InlineData("000000g0")]
    public void Invalid_Digits_In_Later_Bytes_Are_Rejected(string hex)
    {
        Action decode = () => BleValueDecoder.DecodeUnsignedLittleEndian(hex, hex.Length / 2);
        decode.Should().ThrowExactly<FormatException>();
    }

    [Fact]
    public void Wrong_Byte_Length_Identifies_The_Value()
    {
        Action decode = () => BleValueDecoder.DecodeByte("0001", "a battery value");
        decode.Should().ThrowExactly<FormatException>().WithMessage("*battery value*");
    }
}
