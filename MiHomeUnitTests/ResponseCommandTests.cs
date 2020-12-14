using MiHomeLib.Commands;
using Xunit;

namespace MiHomeUnitTests
{
    public class ResponseCommandTests
    {
        [Theory]
        [InlineData("report", ResponseCommandType.Report)]
        [InlineData("heartbeat", ResponseCommandType.Hearbeat)]
        [InlineData("read_ack", ResponseCommandType.ReadAck)]
        [InlineData("get_id_list_ack", ResponseCommandType.GetIdListAck)]
        [InlineData("aaa", ResponseCommandType.Unknown)]
        public void CheckCommandType(string cmd, ResponseCommandType expected)
        {
            // Arrange
            var str = "{\"cmd\":\"" + cmd + "\",\"model\":\"sensor_ht\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal(expected, rcmd.Command);
        }

        [Fact]
        public void CheckModel()
        {
            // Arrange
            var str = "{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal("plug", rcmd.Model);
        }

        [Fact]
        public void CheckSid()
        {
            // Arrange
            var str = "{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal("158d0001826509", rcmd.Sid);
        }

        [Fact]
        public void CheckShortId()
        {
            // Arrange
            var str = "{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal(11109, rcmd.ShortId);
        }

        [Fact]
        public void CheckData()
        {
            // Arrange
            var str = "{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal("{\"temperature\":\"2246\"}", rcmd.Data);
        }

        [Theory]
        [InlineData("{\"cmd\":\"heartbeat\",\"model\":\"gateway\",\"sid\":\"34ce0088db36\",\"short_id\":\"0\",\"token\":\"VPNvuBhwmeWHCbyG\",\"data\":\"{\\\"ip\\\":\\\"192.168.1.12\\\"}\"}", "VPNvuBhwmeWHCbyG")]
        [InlineData("{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}", null)]
        public void CheckToken(string str, string token)
        {
            // Act 
            var rcmd = ResponseCommand.FromString(str);
            // Assert
            Assert.Equal(token, rcmd.Token);
        }
    }
}