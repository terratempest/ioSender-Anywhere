using Avalonia.Media;
using CNC.App;
using CNC.Core;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerColors
{
    public static Color ToColor(this UiColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static Color ResolveCutColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.CutMotionColor, cfg.BlackBackground, UiColor.Red, theme.Cut);

    public static Color ResolveRapidColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.RapidMotionColor, cfg.BlackBackground, UiColor.LightPink, theme.Rapid);

    public static Color ResolveRetractColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.RetractMotionColor, cfg.BlackBackground, UiColor.Green, theme.Retract);

    public static Color ResolveGridColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.GridColor, cfg.BlackBackground, UiColor.Gray, theme.Grid);

    public static Color ResolveHighlightColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.HighlightColor, cfg.BlackBackground, UiColor.Crimson, theme.Highlight);

    public static Color ResolveToolColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        ResolveMotionColor(cfg.ToolOriginColor, cfg.BlackBackground, UiColor.Green, theme.Tool);

    static Color ResolveMotionColor(UiColor configured, bool darkBackground, UiColor darkFallback, Color themeColor)
    {
        if (darkBackground && configured.R < 32 && configured.G < 32 && configured.B < 32)
            return darkFallback.ToColor();

        if (IsDefaultLike(configured))
            return themeColor;

        return configured.ToColor();
    }

    static bool IsDefaultLike(UiColor color) =>
        IsSame(color, UiColor.Red)
        || IsSame(color, UiColor.LightPink)
        || IsSame(color, UiColor.Green)
        || IsSame(color, UiColor.Gray)
        || IsSame(color, UiColor.Crimson);

    static bool IsSame(UiColor left, UiColor right) =>
        left.R == right.R && left.G == right.G && left.B == right.B && left.A == right.A;
}
