using Avalonia;
using Avalonia.X11;
using ioSender.Services;
using System;

namespace ioSender;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Console.Error.WriteLine(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine(e.Exception);
            e.SetObserved();
        };

        try
        {
            EarlyStartupBanner.Show();
            EarlyStartupBanner.ReportProgress("Starting Application...", 10);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            EarlyStartupBanner.Close();
            Console.Error.WriteLine(ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => ConfigureLinuxX11Rendering(AppBuilder.Configure<App>()
            .UsePlatformDetect())
            .WithInterFont()
            .LogToTrace();

    static AppBuilder ConfigureLinuxX11Rendering(AppBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
            return builder;

        return builder.With(new X11PlatformOptions
        {
            RenderingMode =
            [
                X11RenderingMode.Glx,
                X11RenderingMode.Egl,
                X11RenderingMode.Software,
            ],
            EnableDrawnDecorations = true,
            UseDBusMenu = false,
        });
    }

}
