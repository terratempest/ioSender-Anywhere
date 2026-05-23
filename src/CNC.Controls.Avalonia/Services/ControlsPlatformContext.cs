using CNC.App;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.Services;

public static class ControlsPlatformContext
{
    public static IExternalEditor? ExternalEditor { get; set; }
    public static AppConfigService? AppConfig { get; set; }
    public static GrblCommandRouter? CommandRouter { get; set; }
}
