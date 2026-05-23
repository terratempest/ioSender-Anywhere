using CNC.App;
using CNC.Core;
using CNC.GCode;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>App-wide hooks for the 3D G-code viewer (set from ioSender host).</summary>
public static class GCodeViewerContext
{
    public static AppConfigService? AppConfig { get; set; }

    public static GrblViewModel? Grbl { get; set; }

    /// <summary>Returns parsed tokens for the active program (set by ioSender workspace host).</summary>
    public static Func<IReadOnlyList<GCodeToken>>? GetProgramTokens { get; set; }

    public static GCodeViewerConfig Settings =>
        AppConfig?.Base.GCodeViewer ?? new GCodeViewerConfig();
}
