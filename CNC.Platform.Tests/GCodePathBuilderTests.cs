using System.Reflection;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.Platform.Tests;

public sealed class GCodePathBuilderTests
{
    [Fact]
    public void ExplicitStart_ToOrigin_EmitsRapidSegment()
    {
        var tokens = Parse("G21 G90", "G0 X0 Y0");

        var result = GCodePathBuilder.Build(tokens, new Point3D(10d, 0d, 0d));

        Assert.Equal([new NumericVector3(10f, 0f, 0f), new NumericVector3(0f, 0f, 0f)], result.Segments.Rapid);
        Assert.Empty(result.Segments.Cut);
    }

    [Fact]
    public void OriginStart_ToOrigin_EmitsNoSegment()
    {
        var tokens = Parse("G21 G90", "G0 X0 Y0");

        var result = GCodePathBuilder.Build(tokens, new Point3D());

        Assert.Empty(result.Segments.Rapid);
        Assert.Empty(result.Segments.Cut);
        Assert.Empty(result.Segments.Retract);
    }

    [Fact]
    public void OriginStart_FirstRealMove_EmitsOnlyProgramPath()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0");

        var result = GCodePathBuilder.Build(tokens, new Point3D());

        Assert.Equal([new NumericVector3(0f, 0f, 0f), new NumericVector3(1f, 0f, 0f)], result.Segments.Cut);
        Assert.Empty(result.Segments.Rapid);
        Assert.Empty(result.Segments.Retract);
    }

    [Fact]
    public void ExecutedCut_ExcludesCurrentLine()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0", "G1 X3 Y0");

        var points = GCodePathBuilder.BuildExecutedCut(tokens, new Point3D(), throughLineNumber: 3);

        Assert.Equal([new NumericVector3(0f, 0f, 0f), new NumericVector3(1f, 0f, 0f)], points);
    }

    [Fact]
    public void ExecutedCut_ReturnsNoPointsWithoutCurrentLine()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0");

        var points = GCodePathBuilder.BuildExecutedCut(tokens, new Point3D(), throughLineNumber: 0);

        Assert.Empty(points);
    }

    [Fact]
    public void CompletedCut_UsesCompletedLineNumbersOnly()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0", "G1 X3 Y0");

        var points = GCodePathBuilder.BuildCompletedCut(tokens, new Point3D(), new HashSet<uint> { 2 });

        Assert.Equal([new NumericVector3(0f, 0f, 0f), new NumericVector3(1f, 0f, 0f)], points);
    }

    [Fact]
    public void CompletedCut_SkippedMotionsStillAdvanceInterpreterPosition()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0");

        var points = GCodePathBuilder.BuildCompletedCut(tokens, new Point3D(), new HashSet<uint> { 3 });

        Assert.Equal([new NumericVector3(1f, 0f, 0f), new NumericVector3(2f, 0f, 0f)], points);
    }

    [Fact]
    public void CompletedCut_SkippedRapidStillAdvancesInterpreterPosition()
    {
        var tokens = Parse("G21 G90 F100", "G0 X5 Y0", "G1 X6 Y0");

        var points = GCodePathBuilder.BuildCompletedCut(tokens, new Point3D(), new HashSet<uint> { 3 });

        Assert.Equal([new NumericVector3(5f, 0f, 0f), new NumericVector3(6f, 0f, 0f)], points);
    }

    [Fact]
    public void CompletedCut_UsesGeneratedProgramLineNumbers()
    {
        var original = GrblInfo.UseLinenumbers;
        SetUseLinenumbers(true);

        try
        {
            GCodeFileService.Instance.LoadFromLines(
                ["G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0"],
                @"C:\tmp\viewer-line-number-test.nc");

            var completedLine = GCodeFileService.Instance.Data.Single(block => block.DisplayData == "G1X1Y0").LineNum;
            var points = GCodePathBuilder.BuildCompletedCut(
                GCodeFileService.Instance.Tokens,
                new Point3D(),
                new HashSet<uint> { completedLine });

            Assert.Equal(20u, completedLine);
            Assert.Equal([new NumericVector3(0f, 0f, 0f), new NumericVector3(1f, 0f, 0f)], points);
        }
        finally
        {
            GCodeFileService.Instance.Close();
            SetUseLinenumbers(original);
        }
    }

    static IReadOnlyList<GCodeToken> Parse(params string[] blocks)
    {
        var parser = new GCodeParser();
        foreach (var block in blocks)
        {
            var line = block;
            Assert.True(parser.ParseBlock(ref line, quiet: false));
        }

        return parser.Tokens;
    }

    static void SetUseLinenumbers(bool value)
    {
        var property = typeof(GrblInfo).GetProperty(
            nameof(GrblInfo.UseLinenumbers),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(property);
        property.SetValue(null, value);
    }
}
