using CNC.Core;

namespace CNC.Platform.Tests;

public class PortSettingsParserTests
{
    [Theory]
    [InlineData("COM3:115200,N,8,1", "COM3", 115200)]
    [InlineData("/dev/ttyACM0:115200,N,8,1", "/dev/ttyACM0", 115200)]
    public void Parse_valid_port_strings(string input, string port, int baud)
    {
        var settings = PortSettingsParser.Parse(input);
        Assert.Equal(port, settings.PortName);
        Assert.Equal(baud, settings.BaudRate);
    }

    [Fact]
    public void Parse_throws_on_invalid()
    {
        Assert.Throws<SerialPortConfigurationException>(() =>
            PortSettingsParser.Parse("COM1:bad,N,8,1"));
    }
}
