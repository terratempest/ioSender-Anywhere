using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Config;
using CNC.Core;
using GrblConfigApp.Services;

namespace GrblConfigApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var platform = PlatformBootstrap.Create();
        var appConfig = new AppConfigService(platform.PathService);
        appConfig.InitializePaths(AppContext.BaseDirectory);
        appConfig.EnsureLoaded();
        AppTheme.Apply(appConfig.Base.Theme);

        Comms.UiDispatcher = platform.UiDispatcher;
        UiThread.Capture();
        AvaloniaGrblUi.Configure();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(platform, appConfig);

        base.OnFrameworkInitializationCompleted();
    }
}
