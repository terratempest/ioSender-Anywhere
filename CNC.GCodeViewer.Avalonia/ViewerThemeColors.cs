using Avalonia;
using Avalonia.Media;

namespace CNC.GCodeViewer.Avalonia;

internal readonly record struct ViewerThemeColors(
    Color Background,
    Color Cut,
    Color Rapid,
    Color Retract,
    Color Grid,
    Color GridMinor,
    Color GridMajor,
    Color Highlight,
    Color Tool,
    Color WorkEnvelope)
{
    public static ViewerThemeColors Current() => new(
        ThemeColor("IoSenderViewerBackgroundColor", Color.FromRgb(16, 16, 16)),
        ThemeColor("IoSenderViewerCutColor", Colors.Red),
        ThemeColor("IoSenderViewerRapidColor", Colors.LightPink),
        ThemeColor("IoSenderViewerRetractColor", Colors.Green),
        ThemeColor("IoSenderViewerGridColor", Colors.Gray),
        ThemeColor("IoSenderViewerGridMinorColor", Color.FromRgb(64, 64, 64)),
        ThemeColor("IoSenderViewerGridMajorColor", Colors.Gray),
        ThemeColor("IoSenderViewerHighlightColor", Colors.Crimson),
        ThemeColor("IoSenderViewerToolColor", Colors.Green),
        ThemeColor("IoSenderViewerWorkEnvelopeColor", Colors.DarkBlue));

    static Color ThemeColor(string key, Color fallback)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true)
        {
            if (value is Color color)
                return color;

            if (value is ISolidColorBrush brush)
                return brush.Color;
        }

        return fallback;
    }
}
