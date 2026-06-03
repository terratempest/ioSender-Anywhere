using Avalonia.Media;
using CNC.Controls.Avalonia.Converters;
using CNC.Core;
using System.Globalization;

namespace CNC.Platform.Tests;

public sealed class HomedStateToColorConverterTests
{
    [Theory]
    [InlineData(HomedState.Unknown)]
    [InlineData(HomedState.NotHomed)]
    [InlineData(HomedState.Homed)]
    public void Convert_returns_non_transparent_brush_for_homed_states(HomedState state)
    {
        var brush = Convert(state);

        AssertNonTransparent(brush);
    }

    [Fact]
    public void Convert_returns_non_transparent_brush_for_unexpected_value()
    {
        var brush = Convert(null);

        AssertNonTransparent(brush);
    }

    static IBrush Convert(object? value) =>
        Assert.IsAssignableFrom<IBrush>(
            new HomedStateToColorConverter().Convert(value, typeof(IBrush), null, CultureInfo.InvariantCulture));

    static void AssertNonTransparent(IBrush brush)
    {
        if (brush is ISolidColorBrush solid)
            Assert.NotEqual(Colors.Transparent, solid.Color);
    }
}
