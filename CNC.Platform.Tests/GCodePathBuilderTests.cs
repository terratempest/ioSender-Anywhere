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
}
