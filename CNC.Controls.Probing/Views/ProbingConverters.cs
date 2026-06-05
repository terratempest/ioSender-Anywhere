using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CNC.Controls.Probing;

public sealed class OriginToX0Y0Converter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ProbeOrigin o && o == ProbeOrigin.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ProbeOrigin.None : ProbeOrigin.Center;
}

public sealed class OriginToCurrentPosConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ProbeOrigin o && o == ProbeOrigin.CurrentPos;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ProbeOrigin.CurrentPos : ProbeOrigin.None;
}

public sealed class ProbingMacroActiveBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ProbingMacro { Id: > 0 } ? Brushes.Salmon : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
