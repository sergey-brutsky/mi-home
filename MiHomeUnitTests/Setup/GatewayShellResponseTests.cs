using System;
using System.IO;
using System.Linq;
using MiHomeSetup;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class GatewayShellResponseTests
{
    private const string Marker = "0123456789abcdef0123456789abcdef";
    private const string Model = "lumi.gateway.mgl03";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Busybox_1_22_1_Gateway_Transcript_Returns_The_Model(bool useCrLf)
    {
        const string transcript = """
            # printf '\n%s%s\n' 'MIHOME_' '0123456789abcdef0123456789abcdef_BEGIN'; { sed -n 's/^model=//p' /data/miio/device.conf; }; printf '\n%s%s:%s\n' 'MIHOME_' '
            0123456789abcdef0123456789abcdef_END' "$?"

            MIHOME_0123456789abcdef0123456789abcdef_BEGIN

            lumi.gateway.mgl03

            MIHOME_0123456789abcdef0123456789abcdef_END:0

            #
            """;
        var response = useCrLf ? transcript.Replace("\n", "\r\n") : transcript;
        var output = GatewayShell.ParseResponse(response, Marker);
        Assert.Equal(Model, output);
        Assert.Equal(Model, GatewayModelDetector.ParseShellModel(output, "mips"));
    }

    [Theory]
    [InlineData(0, "\r\n")]
    [InlineData(1, "\r\n")]
    [InlineData(2, "\r\n")]
    [InlineData(0, "\n")]
    [InlineData(1, "\n")]
    [InlineData(2, "\n")]
    public void Model_Result_Is_Separated_From_Missing_Normal_Or_Wrapped_Echo(int echoStyle, string newline)
    {
        var command = GatewayShell.BuildCommand(GatewayProfile.Resolve(Model).ModelCommand, Marker);
        var echo = echoStyle switch
        {
            0 => "",
            1 => command + newline,
            _ => string.Join(newline, Enumerable.Range(0, (command.Length + 69) / 70)
                .Select(index => command.Substring(index * 70, Math.Min(70, command.Length - index * 70)))) + newline
        };
        var response = echo + newline + "MIHOME_" + Marker + "_BEGIN" + newline +
            Model + newline + newline + "MIHOME_" + Marker + "_END:0" + newline + "# ";
        var output = GatewayShell.ParseResponse(response, Marker);
        Assert.Equal(Model, output);
        Assert.Equal(Model, GatewayModelDetector.ParseShellModel(output, "mips"));
    }

    [Fact]
    public void Wire_Command_Echo_Does_Not_Contain_Complete_Result_Markers()
    {
        var command = GatewayShell.BuildCommand("echo test", Marker);
        Assert.DoesNotContain("MIHOME_" + Marker, command);
    }

    [Fact]
    public void Upload_Command_With_Result_Markers_Fits_The_Gateway_Input_Line()
    {
        var chunk = Convert.ToBase64String(new byte[GatewayInstaller.UploadChunkSize]);
        var command = "echo '" + chunk + "' | base64 -d >> /data/openmiio_agent.mihome-" + Marker;
        Assert.True(GatewayShell.BuildCommand(command, Marker).Length + 2 < 1024);
    }

    [Fact]
    public void Empty_Command_Output_Is_Valid()
    {
        var response = "\nMIHOME_" + Marker + "_BEGIN\n\nMIHOME_" + Marker + "_END:0\n# ";
        Assert.Equal("", GatewayShell.ParseResponse(response, Marker));
    }

    [Fact]
    public void Nonzero_Exit_Status_Still_Fails()
    {
        var response = "\nMIHOME_" + Marker + "_BEGIN\nfailed\nMIHOME_" + Marker + "_END:3\n# ";
        Assert.Throws<IOException>(() => GatewayShell.ParseResponse(response, Marker));
    }

    [Theory]
    [InlineData("\nMIHOME_{0}_END:0\n# ")]
    [InlineData("\nMIHOME_{0}_BEGIN\nvalue\n# ")]
    [InlineData("\nMIHOME_{0}_BEGIN\nvalue\nMIHOME_{0}_END:invalid\n# ")]
    [InlineData("\nMIHOME_{0}_BEGIN\nvalue\nMIHOME_{0}_END:0")]
    public void Incomplete_Or_Invalid_Framing_Is_Rejected(string response) =>
        Assert.Throws<InvalidDataException>(() => GatewayShell.ParseResponse(string.Format(response, Marker), Marker));
}
