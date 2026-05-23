using CNC.App;

namespace ioSender.Services;

public static class AppHostContext
{
    public static PlatformServices Platform { get; private set; } = null!;

    public static AppConfigService AppConfig { get; private set; } = null!;

    public static string[] StartupArgs { get; private set; } = [];

    public static void Initialize(PlatformServices platform, AppConfigService appConfig, string[]? startupArgs = null)
    {
        Platform = platform;
        AppConfig = appConfig;
        StartupArgs = startupArgs ?? [];
    }
}
