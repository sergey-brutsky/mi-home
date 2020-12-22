using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class MiioPacketTests
    {
        [Fact]
        public void MiioPacket_Returns_Valid_Props()
        {
            // Arrange
            // hello reponse for zhimi.humidifier.v1 with serial number a850
            var hex = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";

            // Act
            var miioPacket = new MiioPacket(hex);

            // Assert
            Assert.Equal("0404", miioPacket.GetDeviceType());
            Assert.Equal("a850", miioPacket.GetSerial());
            Assert.Equal("5d3a2f018c90097a850558c35c953b77", miioPacket.GetChecksum());
        }

        [Fact]
        public void BuildMessage_Returns_Valid_Hex_String()
        {
            // Arrange
            var hex = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c123b77";
            var msg = "{\"id\": 1, \"method\": \"set_power\", \"params\": [\"on\"]}";
            var miioPacket = new MiioPacket(hex);

            // Act
            var packet = miioPacket.BuildMessage(msg, "5d3a2f018c90097a850558c35c953b77");


            // Assert
            Assert.Equal("21310060000000000404a850002e58734a31baf3a300c3bb4179fe1b910bd69d2fa405db0b664dc87c543b16c63c69e5429f4825298687cf6194c23fdb0fbed9ed7846dbd1dd4e258218e3f922b632c3ee62af7240530a0757d9d5ec8febcd7b", packet);
        }

        [Fact]
        public void GetResponseData_Returns_Valid_Response()
        {
            // Arrange
            var hex = "21310040000000000404a850002e5b830afd260f87e33eed4788eb2c5f1b0237da93c178df54ec4a95ee3e952f5c8a73a0d9ad1a8fec934087977f497a8f7eb5";
            var miioPacket = new MiioPacket(hex);

            // Act
            var packet = miioPacket.GetResponseData("5d3a2f018c90097a850558c35c953b77");


            // Assert
            Assert.Equal("{\"result\":[\"ok\"],\"id\":1}", packet);
        }
    }
}