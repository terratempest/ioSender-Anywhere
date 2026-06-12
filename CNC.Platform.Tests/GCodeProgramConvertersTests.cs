using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CNC.Controls.Avalonia.Converters;

namespace CNC.Platform.Tests;

public sealed class GCodeProgramConvertersTests
{
    [Theory]
    [InlineData("*", null)]
    [InlineData("BRK *", null)]
    [InlineData("@", "#FF2E7D32")]
    [InlineData("pending", "#FFC76B00")]
    public void Status_background_distinguishes_transport_current_and_pending(string sent, string? expected)
    {
        var brush = ConvertBrush(new GCodeLineStatusBrushConverter(), sent);

        if (expected == null)
        {
            Assert.Same(Brushes.Transparent, brush);
            return;
        }

        Assert.Equal(Color.Parse(expected), SolidColor(brush));
    }

    [Theory]
    [InlineData("*", false)]
    [InlineData("@", false)]
    [InlineData("pending", false)]
    [InlineData("ok", true)]
    [InlineData("ok foo", true)]
    [InlineData("BRK ok", true)]
    public void Completed_state_only_uses_machine_completion_marker(string sent, bool expected)
    {
        var actual = new GCodeCompletedConverter().Convert(
            sent,
            typeof(bool),
            null,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, actual);
    }

    static IBrush ConvertBrush(IValueConverter converter, string sent) =>
        Assert.IsAssignableFrom<IBrush>(converter.Convert(sent, typeof(IBrush), null, CultureInfo.InvariantCulture));

    static Color SolidColor(IBrush brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
