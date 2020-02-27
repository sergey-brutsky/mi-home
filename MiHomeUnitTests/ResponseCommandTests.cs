using MiHomeLib.Commands;
using Xunit;

namespace MiHomeUnitTests
{
    public class ResponseCommandTests
    {
        [Fact]
        public void Test1()
        {
            var rcmd = ResponseCommand.FromString("{\"cmd\":\"report\",\"model\":\"sensor_ht\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}");
            var a = 1;
            Assert.True(true);
        }
    }
}