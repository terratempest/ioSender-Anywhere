using CNC.GCode;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class GCodeParserCannedCycleTests
{
    [Fact]
    public void LegacyGrbl_rejects_g81_by_default()
    {
        var parser = new GCodeParser { Dialect = Dialect.Grbl };
        var block = "G98G81X8.93Y56.07Z-1.5R44.5F200";

        Assert.Throws<GCodeException>(() => parser.ParseBlock(ref block, quiet: false));
    }

    [Fact]
    public void GrblHal_can_parse_compact_g81_canned_cycle()
    {
        var parser = new GCodeParser { Dialect = Dialect.GrblHAL };
        var block = "G98G81X8.93Y56.07Z-1.5R44.5F200";

        Assert.True(parser.ParseBlock(ref block, quiet: false));
        Assert.Contains(parser.Tokens, token => token.Command == Commands.G98);
        Assert.Contains(parser.Tokens, token => token.Command == Commands.G81);
    }

    [Fact]
    public void LegacyGrbl_rejects_other_canned_cycles()
    {
        var parser = new GCodeParser { Dialect = Dialect.Grbl };
        var block = "G83X8.93Y56.07Z-1.5R44.5Q0.5F200";

        Assert.Throws<GCodeException>(() => parser.ParseBlock(ref block, quiet: false));
    }

    [Fact]
    public void Job_bounds_can_be_calculated_for_allowed_g81_cycle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"iosender-g81-{Guid.NewGuid():N}.nc");
        File.WriteAllLines(path, new[]
        {
            "G21G90",
            "G0Z50",
            "G98G81X8.93Y56.07Z-1.5R44.5F200",
            "G80",
            "M30"
        });

        try
        {
            var job = new GCodeJob();
            job.Parser.Dialect = Dialect.GrblHAL;

            Assert.True(job.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
