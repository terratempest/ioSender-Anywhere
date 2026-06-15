using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CNC.Controls.Avalonia.Converters;
using CNC.Controls.Avalonia.Views;

namespace CNC.Platform.Tests;

public sealed class GCodeProgramConvertersTests
{
    [Theory]
    [MemberData(nameof(NullStatusRows))]
    [InlineData("@")]
    [InlineData("pending")]
    public void Status_cell_background_remains_transparent_for_row_highlight_statuses(string? sent)
    {
        var brush = ConvertBrush(new GCodeLineStatusBrushConverter(), sent);

        Assert.Same(Brushes.Transparent, brush);
    }

    [Theory]
    [InlineData("@", "gcode-current")]
    [InlineData("pending", "gcode-pending")]
    [InlineData("BRK pending", "gcode-pending")]
    [InlineData("ok", "gcode-done")]
    [InlineData("ok foo", "gcode-done")]
    [InlineData("BRK ok", "gcode-done")]
    [InlineData("*", null)]
    public void Row_status_class_tracks_program_status(string sent, string? expected)
    {
        var actual = GCodeListControl.GetRowStatusClass(sent);

        Assert.Equal(expected, actual);
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

    [Theory]
    [InlineData(100, 20, 400, 90)]
    [InlineData(5, 20, 400, 0)]
    [InlineData(395, 20, 400, 385)]
    public void Centered_logical_scroll_offset_uses_target_row_index(
        int index,
        double viewportHeight,
        double maxOffset,
        double expected)
    {
        var actual = GCodeListControl.CalculateCenteredLogicalOffset(
            index,
            viewportHeight,
            maxOffset);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(40, 80, 100, 400, 70)]
    [InlineData(0, 10, 100, 400, 0)]
    [InlineData(380, 90, 100, 400, 400)]
    public void Centered_pixel_scroll_offset_clamps_to_viewport_range(
        double currentOffset,
        double rowCenter,
        double viewportHeight,
        double maxOffset,
        double expected)
    {
        var actual = GCodeListControl.CalculateCenteredPixelOffset(
            currentOffset,
            rowCenter,
            viewportHeight,
            maxOffset);

        Assert.Equal(expected, actual);
    }

    static IBrush ConvertBrush(IValueConverter converter, string? sent) =>
        Assert.IsAssignableFrom<IBrush>(converter.Convert(sent, typeof(IBrush), null, CultureInfo.InvariantCulture));

    public static IEnumerable<object?[]> NullStatusRows =>
    [
        [null],
        ["BRK *"],
    ];
}
