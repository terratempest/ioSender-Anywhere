using System.Reflection;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class GrblInfoCapabilityTests
{
    [Fact]
    public void Newopt_parse_clears_missing_optional_tab_capabilities()
    {
        GrblInfo.ResetOptionalControllerFeatures();
        InvokeInfoProcess("[NEWOPT:TMC=XYZ,PID]");

        Assert.Equal("XYZ", GrblInfo.TrinamicDrivers);
        Assert.True(GrblInfo.HasPIDLog);

        InvokeInfoProcess("[NEWOPT:]");

        Assert.Equal(string.Empty, GrblInfo.TrinamicDrivers);
        Assert.False(GrblInfo.HasPIDLog);
    }

    [Fact]
    public void Optional_capability_reset_clears_values_when_controller_reports_no_newopt()
    {
        GrblInfo.ResetOptionalControllerFeatures();
        InvokeInfoProcess("[NEWOPT:TMC=XYZ,PID]");

        GrblInfo.ResetOptionalControllerFeatures();

        Assert.Equal(string.Empty, GrblInfo.TrinamicDrivers);
        Assert.False(GrblInfo.HasPIDLog);
    }

    static void InvokeInfoProcess(string line)
    {
        var method = typeof(GrblInfo).GetMethod(
            "Process",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method.Invoke(null, [line]);
    }
}
