using CNC.Converters;
using ioSender.Services;

namespace CNC.Platform.Tests;

public sealed class ProgramOpenFilePickerTests
{
    [Fact]
    public void BuildAvaloniaFilters_IncludesBaseAndConverterPatterns()
    {
        GCodeConverterRegistry.RegisterDefaults();

        var filters = ProgramOpenFilePicker.BuildAvaloniaFilters();

        Assert.Contains(filters, f => f.Name == "G-code" && f.Patterns?.Contains("*.nc") == true);
        Assert.Contains(filters, f => f.Name == "Excellon files" && f.Patterns?.Contains("*.drl") == true);
        Assert.Contains(filters, f => f.Name == "HPGL files" && f.Patterns?.Contains("*.plt") == true);
    }

    [Fact]
    public void BuildWin32Filter_UsesCommonDialogNullSeparatedFormat()
    {
        GCodeConverterRegistry.RegisterDefaults();

        var filter = ProgramOpenFilePicker.BuildWin32Filter();

        Assert.Contains("G-code\0*.nc;*.ngc;*.gcode;*.tap;*.cnc;*.txt\0", filter);
        Assert.Contains("Excellon files\0*.drl;*.xln\0", filter);
        Assert.Contains("HPGL files\0*.plt\0", filter);
        Assert.EndsWith("\0\0", filter);
    }
}
