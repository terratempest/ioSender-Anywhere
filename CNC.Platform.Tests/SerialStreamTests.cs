using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class SerialStreamTests
{
    [Fact]
    public void CreateOpenErrorMessage_includes_inner_exception_message()
    {
        var message = SerialStream.CreateOpenErrorMessage(
            "COM3",
            new InvalidOperationException("inner detail"),
            isLinux: false);

        Assert.Contains("Unable to open serial port 'COM3'.", message);
        Assert.Contains("inner detail", message);
    }

    [Fact]
    public void CreateOpenErrorMessage_adds_linux_permission_guidance()
    {
        var message = SerialStream.CreateOpenErrorMessage(
            "/dev/ttyUSB0",
            new UnauthorizedAccessException("Permission denied"),
            isLinux: true);

        Assert.Contains("Permission denied", message);
        Assert.Contains("udev", message);
        Assert.Contains("Reconnect the USB device", message);
        Assert.Contains("dialout", message);
    }
}
