using System.Globalization;
using Avalonia.Data.Converters;
using CNC.Core;
using CNC.Core.Geometry;

namespace CNC.Controls.Config;

public class LogicalNotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value == null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class GrblStateToBooleanConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count == 2 && values[0] is GrblState state && values[1] is GrblStates expected && state.State == expected;

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PositionToStringConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0 || values[0] is not Position pos)
            return string.Empty;

        var format = values.Count > 1 && values[1] is string s ? s : "####0.000";

        return GrblInfo.NumAxes switch
        {
            4 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format)),
            5 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format),
                pos.B.ToInvariantString(format)),
            6 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format),
                pos.B.ToInvariantString(format), pos.C.ToInvariantString(format)),
            7 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format),
                pos.B.ToInvariantString(format), pos.C.ToInvariantString(format),
                pos.U.ToInvariantString(format)),
            8 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format),
                pos.B.ToInvariantString(format), pos.C.ToInvariantString(format),
                pos.U.ToInvariantString(format), pos.V.ToInvariantString(format)),
            9 => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format), pos.A.ToInvariantString(format),
                pos.B.ToInvariantString(format), pos.C.ToInvariantString(format),
                pos.U.ToInvariantString(format), pos.V.ToInvariantString(format),
                pos.W.ToInvariantString(format)),
            _ => string.Format(GrblInfo.PositionFormatString,
                pos.X.ToInvariantString(format), pos.Y.ToInvariantString(format),
                pos.Z.ToInvariantString(format)),
        };
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
