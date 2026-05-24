using CNC.Platform.Linux;

namespace CNC.Platform.Tests;

public class LinuxSerialPortFilterTests
{
    [Theory]
    [InlineData("ttyUSB0", true)]
    [InlineData("ttyACM0", true)]
    [InlineData("/dev/ttyUSB1", true)]
    [InlineData("ttyS0", true)]
    [InlineData("ttyS1", true)]
    [InlineData("tty0", false)]
    [InlineData("tty1", false)]
    [InlineData("/dev/tty2", false)]
    [InlineData("tty", false)]
    [InlineData("ttyprintk", false)]
    public void IsRelevantDevice_classifies_ports(string name, bool expected) =>
        Assert.Equal(expected, LinuxSerialPortFilter.IsRelevantDevice(name));

    [Fact]
    public void NormalizeDevicePath_adds_dev_prefix()
    {
        Assert.Equal("/dev/ttyUSB0", LinuxSerialPortFilter.NormalizeDevicePath("ttyUSB0"));
        Assert.Equal("/dev/ttyACM0", LinuxSerialPortFilter.NormalizeDevicePath("/dev/ttyACM0"));
    }
}
