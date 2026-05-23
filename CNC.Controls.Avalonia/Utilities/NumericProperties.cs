using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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

    public static string MetricFormat => NumberFormatInfo.CurrentInfo.NegativeSign + GrblConstants.FORMAT_METRIC;
    public static string ImperialFormat => NumberFormatInfo.CurrentInfo.NegativeSign + GrblConstants.FORMAT_IMPERIAL;

    public NumericProperties() => Parse(MetricFormat);

    public void Parse(string format)
    {
        AllowSign = format.StartsWith('-');
        DisplayFormat = AllowSign ? format[1..] : format;
        Length = format.Length - (AllowSign ? 1 : 0);
        AllowDP = format.Contains('.');
        Precision = AllowDP ? Length - format.LastIndexOf('.') - (AllowSign ? 0 : 1) : 0;
        Styles = (AllowDP ? NumberStyles.AllowDecimalPoint : NumberStyles.None) |
                 (AllowSign ? NumberStyles.AllowLeadingSign : NumberStyles.None);
    }

    public static void OnFormatChanged(AvaloniaObject d, NumericProperties np, string format)
    {
        np.Parse(format);
        if (d is Control control)
            control.Width = UIUtils.MeasureText("".PadRight(np.Length, '9'), control).Width + (d is Controls.NumericTextBox ? 12 : 20);
    }

    public static bool IsStringNumeric(string value, NumericProperties np)
    {
        var ok = true;
        var len = value.Length;
        var i = 0;
        var dp = -1;

        if (np.AllowSign && value.StartsWith(NumberFormatInfo.CurrentInfo.NegativeSign))
            i++;

        for (; i < len; i++)
        {
            if (np.AllowDP && dp == -1 && value[i] == '.')
                dp = i;
            else
                ok &= char.IsDigit(value[i]);
        }

        if (ok && dp >= 0)
            ok = len - dp - 1 <= np.Precision;

        return ok && len <= np.Length;
    }
}
