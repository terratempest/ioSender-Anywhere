using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Platform.Abstractions;
using ioSender.ViewModels;

namespace ioSender.Services;

/// <summary>Concrete composition root for app-wide UI services.</summary>
public sealed class AppSession
{
    readonly ConsoleLogConfigSync _consoleLogConfigSync;

    public AppSession(PlatformServices platform, AppConfigService appConfig)
    {
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        AppConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        Program = new ProgramService();
        Connection = new ConnectionService(platform.SerialPortDiscovery, platform.UiDispatcher);
        ConnectionCoordinator = new MachineConnectionCoordinator(Connection);
        CommandRouter = new GrblCommandRouter(Program);
        MachineCommands = new MachineCommandService(CommandRouter);
        MainWindow = new MainWindowViewModel(this);
        _consoleLogConfigSync = ConsoleLogConfigSync.Attach(appConfig.Base, Grbl);
        GameController = new SdlGameControllerService(appConfig, platform, Grbl);

        Program.Model = Grbl;
        CNC.Core.Grbl.GrblViewModel = Grbl;
        MachineCommands.Attach(Grbl);
        GameController.Start();
    }

    public PlatformServices Platform { get; }

    public AppConfigService AppConfig { get; }

    public ProgramService Program { get; }

    public ConnectionService Connection { get; }

    public MachineConnectionCoordinator ConnectionCoordinator { get; }

    public MachineConnectionInitializer ConnectionInitializer { get; } = new();

    public GrblCommandRouter CommandRouter { get; }

    public MachineCommandService MachineCommands { get; }

    public MainWindowViewModel MainWindow { get; }

    public SdlGameControllerService GameController { get; }

    public GrblViewModel Grbl => MainWindow.Grbl;

    public void Disconnect()
    {
        ConnectionInitializer.Unregister();
        ConnectionCoordinator.Detach(Grbl);
        GameController.Cancel();
    }

    public void Shutdown()
    {
        _consoleLogConfigSync.Dispose();
        GameController.Dispose();
    }
}
