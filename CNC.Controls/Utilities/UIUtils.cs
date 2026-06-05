using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CNC.Controls.Avalonia.Utilities;

public static class UIUtils
{
    public static IEnumerable<T> FindLogicalChildren<T>(Control? root) where T : Control
    {
        if (root == null)
            yield break;

        foreach (var child in root.GetVisualChildren().OfType<Control>())
        {
            if (child is T match)
                yield return match;

            foreach (var nested in FindLogicalChildren<T>(child))
                yield return nested;
        }
    }

    public static Size MeasureText(string text, Control control)
    {
        var typeface = control is TextBlock tb
            ? new Typeface(tb.FontFamily ?? FontFamily.Default, tb.FontStyle, tb.FontWeight)
            : new Typeface(FontFamily.Default);

        var fontSize = control is TextBlock tb2 ? tb2.FontSize : 12d;

        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black);
        return new Size(formatted.Width, formatted.Height);
    }
}
