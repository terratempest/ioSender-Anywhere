using System.Collections;
using System.Globalization;
using System.Text;
using Avalonia.Data;
using Avalonia.Data.Converters;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe;

public static class LatheConverters
{
    public static bool IsMetric = true;

    public static readonly StringCollectionToTextConverter StringCollectionToText = new();
    public static readonly SideToInsideBoolConverter SideToInsideBool = new();
    public static readonly SideToOutsideBoolConverter SideToOutsideBool = new();
    public static readonly SideToIsEnabledConverter SideToIsEnabled = new();
    public static readonly SideToStringConverter SideToString = new();
    public static readonly ToolToRoundedBoolConverter ToolToRoundedBool = new();
    public static readonly ToolToChamferedBoolConverter ToolToChamferedBool = new();
    public static readonly ToolToLabelStringConverter ToolToLabelString = new();
    public static readonly TaperTypeToBoolConverter TaperTypeToBool = new();
    public static readonly CncMeasureConverter CncMeasure = new();
    public static readonly LatheModeRadiusBoolConverter LatheModeRadiusBool = new();
    public static readonly LatheModeDiameterBoolConverter LatheModeDiameterBool = new();
}

public sealed class LatheModeRadiusBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LatheMode mode && mode == LatheMode.Radius;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? LatheMode.Radius : LatheMode.Diameter;
}

public sealed class LatheModeDiameterBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LatheMode mode && mode == LatheMode.Diameter;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? LatheMode.Diameter : LatheMode.Radius;
}

public sealed class StringCollectionToTextConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0 || values[0] is not IEnumerable items)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var item in items)
        {
            if (item != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(item);
            }
        }
        return sb.ToString();
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public sealed class CncMeasureConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isMetric = parameter is bool b ? b : LatheConverters.IsMetric;
        var f = values.Count > 1 && values[1] is double d ? d : (isMetric ? 1.0d : 25.4d);

        if (values[0] is double v && !double.IsNaN(v))
            return Math.Round(v / f, f == 1.0d ? 3 : 4);

        return double.NaN;
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public sealed class SideToInsideBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Side side && side == Thread.Side.Inside;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Thread.Side.Inside : Thread.Side.Outside;
}

public sealed class SideToOutsideBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Side side && side == Thread.Side.Outside;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Thread.Side.Outside : Thread.Side.Inside;
}

public sealed class SideToIsEnabledConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Side side && side == Thread.Side.Both;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public sealed class SideToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Side side
            ? (side == Thread.Side.Outside ? "Outside diameter:" : "Inside diameter:")
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public sealed class ToolToRoundedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Toolshape shape && shape == Thread.Toolshape.Rounded;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Thread.Toolshape.Rounded : Thread.Toolshape.Chamfer;
}

public sealed class ToolToChamferedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Toolshape shape && shape == Thread.Toolshape.Chamfer;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Thread.Toolshape.Chamfer : Thread.Toolshape.Rounded;
}

public sealed class ToolToLabelStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Thread.Toolshape shape
            ? (shape == Thread.Toolshape.Rounded ? "Radius r:" : "Chamfer a:")
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public sealed class TaperTypeToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ThreadTaper taper && taper != ThreadTaper.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
