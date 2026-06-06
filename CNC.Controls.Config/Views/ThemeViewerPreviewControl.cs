using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CNC.Controls.Config;

public sealed class ThemeViewerPreviewControl : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width < 4 || bounds.Height < 4)
            return;

        var background = ResourceColor("IoSenderViewerBackgroundColor", Color.FromRgb(16, 16, 16));
        var gridMinor = ResourceColor("IoSenderViewerGridMinorColor", Color.FromRgb(53, 53, 53));
        var gridMajor = ResourceColor("IoSenderViewerGridMajorColor", Color.FromRgb(106, 106, 106));
        var cut = ResourceColor("IoSenderViewerCutColor", Colors.Red);
        var rapid = ResourceColor("IoSenderViewerRapidColor", Colors.LightPink);
        var retract = ResourceColor("IoSenderViewerRetractColor", Colors.Green);
        var highlight = ResourceColor("IoSenderViewerHighlightColor", Colors.Crimson);
        var tool = ResourceColor("IoSenderViewerToolColor", Colors.LightGreen);
        var envelope = ResourceColor("IoSenderViewerWorkEnvelopeColor", Colors.DodgerBlue);

        context.FillRectangle(new SolidColorBrush(background), new Rect(bounds.Size));

        var minorPen = new Pen(new SolidColorBrush(gridMinor), 0.6);
        var majorPen = new Pen(new SolidColorBrush(gridMajor), 1.2);
        var step = Math.Max(18, Math.Min(bounds.Width, bounds.Height) / 9);

        for (var x = bounds.Width / 2 % step; x < bounds.Width; x += step)
            context.DrawLine(minorPen, new Point(x, 0), new Point(x, bounds.Height));
        for (var y = bounds.Height / 2 % step; y < bounds.Height; y += step)
            context.DrawLine(minorPen, new Point(0, y), new Point(bounds.Width, y));

        context.DrawLine(majorPen, new Point(bounds.Width * 0.12, bounds.Height * 0.74), new Point(bounds.Width * 0.88, bounds.Height * 0.74));
        context.DrawLine(majorPen, new Point(bounds.Width * 0.18, bounds.Height * 0.18), new Point(bounds.Width * 0.18, bounds.Height * 0.88));

        var envelopeRect = new Rect(bounds.Width * 0.18, bounds.Height * 0.18, bounds.Width * 0.68, bounds.Height * 0.62);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(envelope), 1.4), envelopeRect);

        DrawPolyline(context, new Pen(new SolidColorBrush(cut), 2.4),
        [
            new Point(bounds.Width * 0.24, bounds.Height * 0.66),
            new Point(bounds.Width * 0.36, bounds.Height * 0.42),
            new Point(bounds.Width * 0.50, bounds.Height * 0.58),
            new Point(bounds.Width * 0.67, bounds.Height * 0.34),
            new Point(bounds.Width * 0.78, bounds.Height * 0.58),
        ]);

        DrawPolyline(context, new Pen(new SolidColorBrush(rapid), 1.4),
        [
            new Point(bounds.Width * 0.24, bounds.Height * 0.66),
            new Point(bounds.Width * 0.36, bounds.Height * 0.20),
            new Point(bounds.Width * 0.78, bounds.Height * 0.58),
        ]);

        DrawPolyline(context, new Pen(new SolidColorBrush(retract), 1.8),
        [
            new Point(bounds.Width * 0.78, bounds.Height * 0.58),
            new Point(bounds.Width * 0.78, bounds.Height * 0.28),
        ]);

        context.DrawEllipse(new SolidColorBrush(highlight), null, new Point(bounds.Width * 0.50, bounds.Height * 0.58), 4, 4);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(tool), 2), new Point(bounds.Width * 0.78, bounds.Height * 0.28), 8, 8);
        context.DrawLine(new Pen(new SolidColorBrush(tool), 1.5),
            new Point(bounds.Width * 0.78, bounds.Height * 0.20),
            new Point(bounds.Width * 0.78, bounds.Height * 0.36));
        context.DrawLine(new Pen(new SolidColorBrush(tool), 1.5),
            new Point(bounds.Width * 0.70, bounds.Height * 0.28),
            new Point(bounds.Width * 0.86, bounds.Height * 0.28));
    }

    static void DrawPolyline(DrawingContext context, Pen pen, IReadOnlyList<Point> points)
    {
        for (var i = 1; i < points.Count; i++)
            context.DrawLine(pen, points[i - 1], points[i]);
    }

    Color ResourceColor(string key, Color fallback)
    {
        if (Application.Current?.TryGetResource(key, ActualThemeVariant, out var value) == true)
        {
            if (value is Color color)
                return color;

            if (value is ISolidColorBrush brush)
                return brush.Color;
        }

        return fallback;
    }
}
