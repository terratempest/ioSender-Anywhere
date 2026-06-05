using CNC.App;
using CNC.Core;
using CNC.GCode;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Explicit state needed by the Avalonia G-code viewer.</summary>
public sealed class GCodeViewerSession
{
    public GCodeViewerSession(
        AppConfigService appConfig,
        GrblViewModel grbl,
        Func<IReadOnlyList<GCodeToken>> getProgramTokens)
    {
        AppConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        Grbl = grbl ?? throw new ArgumentNullException(nameof(grbl));
        GetProgramTokens = getProgramTokens ?? throw new ArgumentNullException(nameof(getProgramTokens));
    }

    public AppConfigService AppConfig { get; }

    public GrblViewModel Grbl { get; }

    public Func<IReadOnlyList<GCodeToken>> GetProgramTokens { get; }

    public GCodeViewerConfig Settings => AppConfig.Base.GCodeViewer;
}
