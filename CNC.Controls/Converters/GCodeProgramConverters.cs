using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Converters;

public sealed class GCodeLineStatusBrushConverter : IValueConverter
{
    static readonly IBrush Transparent = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

}

public sealed class GCodeLineNumberForegroundConverter : IValueConverter
{
    static readonly IBrush DefaultBrush = Brush.Parse("#8A8A8A");
    static readonly IBrush ActiveBrush = Brush.Parse("#F0F0F0");
    static readonly IBrush CompletedBrush = Brush.Parse("#606060");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var sent = Normalize(value);
        if (sent is "@" or "pending")
            return ActiveBrush;
        return IsCompleted(sent) ? CompletedBrush : DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    static bool IsCompleted(string sent) =>
        sent is "ok" || sent.StartsWith("ok", StringComparison.Ordinal);

    static string Normalize(object? value) =>
        value?.ToString()?.Replace("BRK ", string.Empty, StringComparison.Ordinal) ?? string.Empty;
}

public sealed class GCodeCompletedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var sent = value?.ToString()?.Replace("BRK ", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return sent is "ok" || sent.StartsWith("ok", StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
