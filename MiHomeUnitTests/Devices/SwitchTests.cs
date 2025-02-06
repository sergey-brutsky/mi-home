using System.Collections.Generic;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using MiHomeLib.Utils;
using Xunit;

namespace MiHomeUnitTests.Devices
{
    public class SwitchTests(MiHomeDeviceFactoryFixture fixture) : IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _fixture = fixture;

        [Fact]
        public void Check_Switch_Click_Raised()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>());

            // Act
            Switch device = _fixture.GetDeviceByCommand<Switch>(cmd);

            bool clickRaised = false;

            device.OnClick += (_, args) => clickRaised = true;

            cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>
                {
                    { "status", "click" },
                });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(clickRaised);
        }

        [Fact]
        public void Check_Switch_DoubleClick_Raised()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>());

            // Act
            Switch device = _fixture.GetDeviceByCommand<Switch>(cmd);

            bool doubleClickRaised = false;

            device.OnDoubleClick += (_, args) => doubleClickRaised = true;

            cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>
                {
                    { "status", "double_click" },
                });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(doubleClickRaised);
        }

        [Fact]
        public void Check_Switch_LongPressClick_Raised()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>());

            // Act
            Switch device = _fixture.GetDeviceByCommand<Switch>(cmd);

            bool longClickRaised = false;

            device.OnLongPress += (_, args) => longClickRaised = true;

            cmd = Helpers
                .CreateCommand("report", "switch", "34ce0067db36", 34308, new Dictionary<string, object>
                {
                    { "status", "long_click_press" },
                });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(longClickRaised);
        }
    }
}