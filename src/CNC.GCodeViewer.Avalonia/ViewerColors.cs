using Avalonia.Media;
using CNC.App;
using CNC.Core;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerColors
{
    public static Color ToColor(this UiColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static Color ResolveCutColor(GCodeViewerConfig cfg) =>
        ResolveMotionColor(cfg.CutMotionColor, cfg.BlackBackground, UiColor.Red);

    public static Color ResolveRapidColor(GCodeViewerConfig cfg) =>
        ResolveMotionColor(cfg.RapidMotionColor, cfg.BlackBackground, UiColor.LightPink);

    public static Color ResolveRetractColor(GCodeViewerConfig cfg) =>
        ResolveMotionColor(cfg.RetractMotionColor, cfg.BlackBackground, UiColor.Green);

    public static Color ResolveGridColor(GCodeViewerConfig cfg) =>
        ResolveMotionColor(cfg.GridColor, cfg.BlackBackground, UiColor.Gray);

    static Color ResolveMotionColor(UiColor configured, bool darkBackground, UiColor darkFallback)
    {
        if (darkBackground && configured.R < 32 && configured.G < 32 && configured.B < 32)
            return darkFallback.ToColor();
        return configured.ToColor();
    }
}
