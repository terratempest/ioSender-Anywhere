using CNC.Platform.Abstractions;

namespace ioSender.Services;

public sealed class PlatformServices
{
    public PlatformServices(
        IUiDispatcher uiDispatcher,
        ISerialPortDiscovery serialPortDiscovery,
        IPathService pathService,
        IExternalEditor externalEditor,
        ISingleInstanceHost singleInstanceHost)
    {
        UiDispatcher = uiDispatcher;
        SerialPortDiscovery = serialPortDiscovery;
        PathService = pathService;
        ExternalEditor = externalEditor;
        SingleInstanceHost = singleInstanceHost;
    }

    public IUiDispatcher UiDispatcher { get; }
    public ISerialPortDiscovery SerialPortDiscovery { get; }
    public IPathService PathService { get; }
    public IExternalEditor ExternalEditor { get; }
    public ISingleInstanceHost SingleInstanceHost { get; }
}
