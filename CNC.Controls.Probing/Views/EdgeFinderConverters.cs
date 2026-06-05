using System.Globalization;
using Avalonia.Data.Converters;

namespace CNC.Controls.Probing;

/// <summary>Shows Z-probe indicator on the selected edge/corner when Probe Z is enabled.</summary>
public sealed class EdgeZProbeVisibleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || parameter == null)
            return false;

        var edge = values[0]?.ToString();
        var probeZ = values[1] is bool z && z;
        return probeZ && string.Equals(edge, parameter.ToString(), StringComparison.InvariantCultureIgnoreCase);
    }
}

public sealed class LogicalAndConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var v in values)
        {
            if (v is not bool b || !b)
                return false;
        }

        return values.Count > 0;
    }
}
