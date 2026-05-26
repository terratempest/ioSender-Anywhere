using System.Globalization;
using Avalonia;
using CNC.Core;

namespace CNC.Controls.Avalonia.Utilities;

public class NumericProperties
{
    public int Length;
    public int Precision;
    public bool AllowDP;
    public bool AllowSign;
    public string DisplayFormat = string.Empty;
    public NumberStyles Styles;

    const string NegativeSign = "-";

    public static string MetricFormat => NegativeSign + GrblConstants.FORMAT_METRIC;
    public static string ImperialFormat => NegativeSign + GrblConstants.FORMAT_IMPERIAL;

    public NumericProperties() => Parse(MetricFormat);

    public void Parse(string format)
    {
        AllowSign = format.StartsWith(NegativeSign, StringComparison.Ordinal);
        DisplayFormat = AllowSign ? format[1..] : format;
        Length = format.Length - (AllowSign ? 1 : 0);
        AllowDP = DisplayFormat.Contains('.', StringComparison.Ordinal);
        Precision = AllowDP ? DisplayFormat.Length - DisplayFormat.LastIndexOf('.') - 1 : 0;
        Styles = (AllowDP ? NumberStyles.AllowDecimalPoint : NumberStyles.None) |
                 (AllowSign ? NumberStyles.AllowLeadingSign : NumberStyles.None);
    }

    public static void OnFormatChanged(AvaloniaObject d, NumericProperties np, string format)
    {
        np.Parse(format);
    }

    public static bool IsStringNumeric(string value, NumericProperties np)
        => IsValidPartialText(value, np);

    public static bool IsValidPartialText(string value, NumericProperties np)
    {
        var len = value.Length;
        var i = 0;
        var dp = -1;

        if (value.StartsWith(NegativeSign, StringComparison.Ordinal))
        {
            if (!np.AllowSign)
                return false;
            i++;
        }

        for (; i < len; i++)
        {
            if (np.AllowDP && dp == -1 && value[i] == '.')
            {
                dp = i;
                continue;
            }

            if (!char.IsDigit(value[i]))
                return false;
        }

        return dp < 0 || len - dp - 1 <= np.Precision;
    }

    public static bool TryParseCommittedText(string value, NumericProperties np, out double result)
    {
        result = double.NaN;
        if (!IsValidPartialText(value, np))
            return false;

        if (value.Length == 0)
            return true;

        if (value == NegativeSign)
            return false;

        if (value is "." or "-.")
            return false;

        return double.TryParse(value, np.Styles, CultureInfo.InvariantCulture, out result);
    }
}
