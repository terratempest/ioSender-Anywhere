using CNC.Controls.Avalonia.Utilities;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class NumericPropertiesTests
{
    [Theory]
    [InlineData("-###0.000", true, "###0.000", 8, true, 3)]
    [InlineData("###0.000", false, "###0.000", 8, true, 3)]
    [InlineData("-####", true, "####", 4, false, 0)]
    [InlineData("####", false, "####", 4, false, 0)]
    public void Parse_maps_signed_decimal_and_integer_format_properties(
        string format,
        bool allowSign,
        string displayFormat,
        int length,
        bool allowDecimalPoint,
        int precision)
    {
        var numeric = new NumericProperties();

        numeric.Parse(format);

        Assert.Equal(allowSign, numeric.AllowSign);
        Assert.Equal(displayFormat, numeric.DisplayFormat);
        Assert.Equal(length, numeric.Length);
        Assert.Equal(allowDecimalPoint, numeric.AllowDP);
        Assert.Equal(precision, numeric.Precision);
    }

    [Fact]
    public void Built_in_formats_are_signed_with_expected_fractional_precision()
    {
        var metric = new NumericProperties();
        metric.Parse(NumericProperties.MetricFormat);

        var imperial = new NumericProperties();
        imperial.Parse(NumericProperties.ImperialFormat);

        Assert.Equal(GrblConstants.FORMAT_METRIC, metric.DisplayFormat);
        Assert.True(metric.AllowSign);
        Assert.True(metric.AllowDP);
        Assert.Equal(3, metric.Precision);

        Assert.Equal(GrblConstants.FORMAT_IMPERIAL, imperial.DisplayFormat);
        Assert.True(imperial.AllowSign);
        Assert.True(imperial.AllowDP);
        Assert.Equal(4, imperial.Precision);
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("12345", true)]
    [InlineData("-123", false)]
    [InlineData("12.3", false)]
    public void IsStringNumeric_enforces_unsigned_integer_format(string value, bool expected)
    {
        var numeric = new NumericProperties();
        numeric.Parse("####");

        Assert.Equal(expected, NumericProperties.IsStringNumeric(value, numeric));
    }

    [Theory]
    [InlineData("-12.34", true)]
    [InlineData("12.34", true)]
    [InlineData("12345.67", true)]
    [InlineData("-12.345", false)]
    [InlineData("12.", true)]
    [InlineData("12..", false)]
    public void IsStringNumeric_enforces_decimal_fractional_precision(string value, bool expected)
    {
        var numeric = new NumericProperties();
        numeric.Parse("-##0.00");

        Assert.Equal(expected, NumericProperties.IsStringNumeric(value, numeric));
    }

    [Theory]
    [InlineData("-", false)]
    [InlineData(".", false)]
    [InlineData("-.", false)]
    [InlineData("", true)]
    public void IsStringNumeric_allows_partial_edit_text_without_treating_it_as_a_number(string value, bool expectedCommitted)
    {
        var numeric = new NumericProperties();
        numeric.Parse("-##0.00");

        Assert.True(NumericProperties.IsStringNumeric(value, numeric));
        Assert.Equal(expectedCommitted, NumericProperties.TryParseCommittedText(value, numeric, out var result));
        if (expectedCommitted)
            Assert.True(double.IsNaN(result));
    }
}
