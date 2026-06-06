using Avalonia.Media;
using CNC.App;
using CNC.Core;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerColors
{
    public static Color ToColor(this UiColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static Color ResolveCutColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Cut;

    public static Color ResolveRapidColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Rapid;

    public static Color ResolveRetractColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Retract;

    public static Color ResolveGridColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Grid;

    public static Color ResolveHighlightColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Highlight;

    public static Color ResolveToolColor(GCodeViewerConfig cfg, ViewerThemeColors theme) =>
        theme.Tool;
}
