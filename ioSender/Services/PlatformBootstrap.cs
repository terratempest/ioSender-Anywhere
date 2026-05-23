using CNC.Platform.Abstractions;

namespace ioSender.Services;

public static class PlatformBootstrap
{
    public static PlatformServices Create()
    {
        var dispatcher = new AvaloniaUiDispatcher();

#if IOSENDER_WINDOWS
        return new PlatformServices(
            dispatcher,
            new CNC.Platform.Windows.WindowsSerialPortDiscovery(),
            new CNC.Platform.Windows.WindowsPathService(),
            new CNC.Platform.Windows.WindowsExternalEditor(),
            new CNC.Platform.Windows.WindowsSingleInstanceHost());
#elif IOSENDER_LINUX
        return new PlatformServices(
            dispatcher,
            new CNC.Platform.Linux.LinuxSerialPortDiscovery(),
            new CNC.Platform.Linux.LinuxPathService(),
            new CNC.Platform.Linux.LinuxExternalEditor(),
            new CNC.Platform.Linux.LinuxSingleInstanceHost());
#else
        throw new PlatformNotSupportedException("ioSender supports Windows and Linux only.");
#endif
    }
}
