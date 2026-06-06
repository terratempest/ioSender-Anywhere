using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Config;
using CNC.Core;
using CNC.Localization;
using ioSender.Services;
using ioSender.Views;

namespace ioSender;

public partial class App : Application
{
    public override void Initialize()
    {
        using var _ = StartupTrace.Measure("App.Initialize");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine(e.Exception);
            e.Handled = true;
        };

        StartupTrace.Mark("Framework initialization completed");
        EarlyStartupBanner.ReportProgress("Preparing application...", 20);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartupBannerWindow? startupBanner = null;

            try
            {
                if (!EarlyStartupBanner.IsActive)
                {
                    startupBanner = new StartupBannerWindow();
                    startupBanner.ReportProgress("Preparing application...", 20);
                    startupBanner.Show();
                }
            }
            catch
            {
                CloseStartupBanner(startupBanner);
                throw;
            }

            Dispatcher.UIThread.Post(
                () => InitializeDesktopApplication(desktop, startupBanner),
                DispatcherPriority.Background);

            base.OnFrameworkInitializationCompleted();
            return;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeDesktopApplication(
        IClassicDesktopStyleApplicationLifetime desktop,
        StartupBannerWindow? startupBanner)
    {
        ReportStartupProgress(startupBanner, "Loading platform services...", 30);
        PlatformServices platform;
        using (StartupTrace.Measure("Platform services"))
            platform = PlatformBootstrap.Create();

        ReportStartupProgress(startupBanner, "Loading configuration...", 40);
        var appConfig = new AppConfigService(platform.PathService);
        using (StartupTrace.Measure("Configuration load"))
        {
            appConfig.InitializePaths(AppContext.BaseDirectory);
            appConfig.EnsureLoaded();
        }

        var startupArgs = desktop.Args ?? [];

        AppHostContext.Initialize(platform, appConfig, startupArgs);
        CNC.Controls.Avalonia.Controls.PopupKeyboardService.TriggerClickCount =
            () => (int)AppHostContext.AppConfig.Base.PopupKeyboardTrigger;

        ReportStartupProgress(startupBanner, "Applying theme...", 52);
        using (StartupTrace.Measure("Theme apply"))
            AppTheme.Apply(appConfig.Base.Theme);

        ReportStartupProgress(startupBanner, "Loading localization...", 60);
        using (StartupTrace.Measure("Localization load"))
        {
            LocalizationBootstrap.Initialize(
                startupArgs,
                appConfig.Base.Locale,
                AppContext.BaseDirectory);
        }

        ReportStartupProgress(startupBanner, "Configuring controls...", 68);
        Comms.UiDispatcher = platform.UiDispatcher;

        UiThread.Capture();

        AvaloniaGrblUi.Configure();

        ReportStartupProgress(startupBanner, "Checking running instances...", 75);
        if (!platform.SingleInstanceHost.TryAcquire())
        {
            CloseStartupBanner(startupBanner);

            var forwarded = string.Join('\n', Environment.GetCommandLineArgs().Skip(1));
            if (!string.IsNullOrWhiteSpace(forwarded))
                platform.SingleInstanceHost.SendMessage(forwarded + '\n');

            Environment.Exit(0);
            return;
        }

        desktop.Exit += (_, _) =>
        {
            CloseStartupBanner(startupBanner);

            if (AppHostContext.Session is not null)
                AppHostContext.Session.Shutdown();
            appConfig.Shutdown();
            appConfig.Save();
        };

        try
        {
            ReportStartupProgress(startupBanner, "Building main window...", 84);
            using (StartupTrace.Measure("App session"))
                AppHostContext.EnsureSession();
            MainWindow mainWindow;
            using (StartupTrace.Measure("MainWindow construction"))
                mainWindow = new MainWindow();
            mainWindow.Opened += (_, _) =>
            {
                StartupTrace.Mark("MainWindow opened");
                ReportStartupProgress(startupBanner, "Opening workspace...", 99);
                CloseStartupBanner(startupBanner);
            };

            desktop.MainWindow = mainWindow;
            ReportStartupProgress(startupBanner, "Showing main window...", 94);
            StartupTrace.Mark("Showing main window");
            mainWindow.Show();
        }
        catch
        {
            CloseStartupBanner(startupBanner);
            throw;
        }

        _ = Task.Run(() =>
        {
            platform.SingleInstanceHost.Listen(message =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (desktop.MainWindow is MainWindow window)
                        window.HandleIpcMessage(message);
                });
            });
        });
    }

    private static void CloseStartupBanner(StartupBannerWindow? startupBanner)
    {
        EarlyStartupBanner.Close();

        if (startupBanner is { IsVisible: true })
            startupBanner.Close();
    }

    private static void ReportStartupProgress(
        StartupBannerWindow? startupBanner,
        string statusText,
        int percent)
    {
        EarlyStartupBanner.ReportProgress(statusText, percent);
        startupBanner?.ReportProgress(statusText, percent);
    }
}
