using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;
using CNC.Utility.GCode;

namespace CNC.Platform.Tests;

public sealed class UtilityGCodeGeneratorTests
{
    [Fact]
    public void Surfacing_OvercutsByToolRadius()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            toolDiameter: 2d,
            materialX: 10d,
            materialY: 6d,
            toolEngagementPercent: 50d,
            cutType: SurfacingCutType.Conventional));

        Assert.Contains("G0 X11 Y-1", lines);
        Assert.Contains("G1 X-1 F100", lines);
        Assert.Contains("G0 X11 Y7", lines);
    }

    [Fact]
    public void Surfacing_DepthPassesAndFinishRepeatsReachFinalDepth()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            targetDepth: -3d,
            stepDownPasses: 3,
            finishPasses: 2));

        Assert.Contains("G1 Z-1 F25", lines);
        Assert.Contains("G1 Z-2 F25", lines);
        Assert.True(lines.Count(line => line == "G1 Z-3 F25") > lines.Count(line => line == "G1 Z-1 F25"));
    }

    [Fact]
    public void Surfacing_ZeroDepthStillHonorsFinishRepeats()
    {
        var singlePass = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(finishPasses: 0));
        var repeated = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(finishPasses: 2));

        Assert.True(repeated.Count(line => line == "G1 Z0 F25") > singlePass.Count(line => line == "G1 Z0 F25"));
    }

    [Fact]
    public void Surfacing_BothUsesCutDepthArcTransitions()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            toolDiameter: 2d,
            materialX: 10d,
            materialY: 2d,
            toolEngagementPercent: 50d));

        var list = lines.ToList();
        var firstPlunge = list.IndexOf("G1 Z0 F25");
        var firstCut = list.IndexOf("G1 X11 F100");
        var firstConnector = list.IndexOf("G3 X11 Y0 I0 J0.5 F100");
        var secondCut = list.IndexOf("G1 X-1 F100");
        var secondConnector = list.IndexOf("G2 X-1 Y1 I0 J0.5 F100");

        Assert.Contains("G91.1", list);
        Assert.True(firstPlunge >= 0);
        Assert.True(firstCut > firstPlunge);
        Assert.True(firstConnector > firstCut);
        Assert.True(secondCut > firstConnector);
        Assert.True(secondConnector > secondCut);
        Assert.DoesNotContain("G0 X11 Y0", list);
        Assert.DoesNotContain("G0 X-1 Y1", list);
    }

    [Fact]
    public void Surfacing_FinalTrackKeepsFullToolEngagementSpacing()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            cutType: SurfacingCutType.Conventional,
            toolDiameter: 2d,
            materialX: 10d,
            materialY: 2.4d,
            toolEngagementPercent: 60d));

        Assert.Contains("G0 X11 Y3.8", lines);
        Assert.DoesNotContain("G0 X11 Y3.4", lines);
    }

    [Fact]
    public void Surfacing_ConventionalUsesOneWayRetractReturns()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            cutType: SurfacingCutType.Conventional,
            toolDiameter: 2d,
            materialX: 10d,
            materialY: 2d,
            toolEngagementPercent: 50d));

        var firstRapid = lines.ToList().IndexOf("G0 X11 Y-1");
        var firstCut = lines.ToList().IndexOf("G1 X-1 F100");
        var secondRapid = lines.ToList().IndexOf("G0 X11 Y0");

        Assert.True(firstRapid >= 0);
        Assert.True(firstCut > firstRapid);
        Assert.True(secondRapid > firstCut);
    }

    [Fact]
    public void Surfacing_EmitsSpindleAndCoolant()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing(
            coolant: new CoolantOptions(Flood: true, Mist: true)));

        Assert.Contains("S12000", lines);
        Assert.Contains("M3", lines);
        Assert.Contains("M7", lines);
        Assert.Contains("M8", lines);
        Assert.Contains("M9", lines);
        Assert.Contains("M5", lines);
    }

    [Fact]
    public void Drilling_EmitsExpandedSingleHoleMoves()
    {
        var lines = UtilityGCodeGenerator.GenerateDrilling(CreateDrilling(dwellSeconds: 0.5d));

        var list = lines.ToList();
        Assert.True(list.IndexOf("G0 Z5") > list.IndexOf("M3"));
        Assert.Contains("G0 X2 Y3", list);
        Assert.Contains("G0 Z1", list);
        Assert.Contains("G1 Z-4 F25", list);
        Assert.Contains("G4 P0.5", list);
        Assert.Contains("G1 Z1 F100", list);
    }

    [Fact]
    public void GeneratedPrograms_ParseAndBuildPreviewPath()
    {
        var surfacingTokens = Parse(UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing()).ToArray());
        var drillingTokens = Parse(UtilityGCodeGenerator.GenerateDrilling(CreateDrilling()).ToArray());

        Assert.True(GCodePathBuilder.Build(surfacingTokens, new Point3D()).MotionCount > 0);
        Assert.True(GCodePathBuilder.Build(drillingTokens, new Point3D()).MotionCount > 0);
    }

    [Fact]
    public void PreviewParser_ParsesGeneratedLinesWithoutActiveProgramService()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(CreateSurfacing());
        var preview = UtilityGCodePreviewParser.Parse(lines, "utility preview");

        Assert.NotEmpty(preview.Tokens);
        Assert.NotEmpty(preview.Blocks);
        Assert.Contains(preview.Blocks, block => block.Data == "G21");
        Assert.True(GCodePathBuilder.Build(preview.Tokens, new Point3D()).MotionCount > 0);
    }

    static SurfacingOptions CreateSurfacing(
        double toolDiameter = 4d,
        double materialX = 12d,
        double materialY = 8d,
        double targetDepth = 0d,
        int stepDownPasses = 1,
        int finishPasses = 0,
        double toolEngagementPercent = 50d,
        SurfacingCutType cutType = SurfacingCutType.Both,
        CoolantOptions? coolant = null) =>
        new(
            UtilityUnits.Metric,
            UtilityOrigin.LowerLeft,
            0d,
            0d,
            0d,
            toolDiameter,
            materialX,
            materialY,
            targetDepth,
            stepDownPasses,
            finishPasses,
            toolEngagementPercent,
            100d,
            25d,
            5d,
            12000,
            coolant ?? new CoolantOptions(false, false),
            SurfacingPassDirection.AlongX,
            cutType);

    static DrillingOptions CreateDrilling(double dwellSeconds = 0d) =>
        new(
            UtilityUnits.Metric,
            5d,
            1d,
            100d,
            25d,
            12000,
            new CoolantOptions(false, true),
            [new DrillHole(2d, 3d, -4d, dwellSeconds)]);

    static IReadOnlyList<GCodeToken> Parse(params string[] blocks)
    {
        var parser = new GCodeParser();
        foreach (var block in blocks)
        {
            var line = block;
            Assert.True(parser.ParseBlock(ref line, quiet: false), block);
        }

        return parser.Tokens;
    }
}
