using System.Reflection;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.OpenGl;
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
    public void Build_LargeLinearPreview_PreservesSegmentsBeyondOldLayerCap()
    {
        const int moves = PathDecimator.MaxVerticesPerLayer / 2 + 1;
        var blocks = new string[moves + 1];
        blocks[0] = "G21 G90 F100";
        for (var i = 1; i <= moves; i++)
            blocks[i] = $"G1 X{i} Y0";

        var result = GCodePathBuilder.Build(Parse(blocks), new Point3D());

        Assert.Equal(moves, result.MotionCount);
        Assert.Equal(moves * 2, result.Segments.Cut.Count);
        Assert.Equal(new NumericVector3(0f, 0f, 0f), result.Segments.Cut[0]);
        Assert.Equal(new NumericVector3(moves, 0f, 0f), result.Segments.Cut[^1]);
    }

    [Fact]
    public void Build_LargeArcPreview_PreservesArcSegmentsBeyondOldLayerCap()
    {
        const int arcMoves = 20_000;
        var blocks = new string[arcMoves + 2];
        blocks[0] = "G21 G90 G17 F100";
        blocks[1] = "G0 X0 Y0";
        for (var i = 0; i < arcMoves; i++)
        {
            var even = (i & 1) == 0;
            blocks[i + 2] = even
                ? "G2 X1 Y0 I0.5 J0"
                : "G2 X0 Y0 I-0.5 J0";
        }

        var result = GCodePathBuilder.Build(Parse(blocks), new Point3D(), arcResolution: 1d);

        Assert.Equal(arcMoves + 1, result.MotionCount);
        Assert.True(result.Segments.Cut.Count > PathDecimator.MaxVerticesPerLayer);
        Assert.True(result.Segments.Cut.Count >= arcMoves * 2);
        Assert.Equal(new NumericVector3(0f, 0f, 0f), result.Segments.Cut[0]);
        Assert.Equal(new NumericVector3(0f, 0f, 0f), result.Segments.Cut[^1]);
    }

    [Fact]
    public void OpenGlLineRenderer_ChunksLinesOnSegmentBoundaries()
    {
        var exact = OpenGlLineRenderer.ChunkVertices(
            OpenGlLineRenderer.MaxVerticesPerUploadChunk,
            ViewerPrimitiveKind.Lines);
        var plusSegment = OpenGlLineRenderer.ChunkVertices(
            OpenGlLineRenderer.MaxVerticesPerUploadChunk + 2,
            ViewerPrimitiveKind.Lines);
        var oddInvalid = OpenGlLineRenderer.ChunkVertices(
            OpenGlLineRenderer.MaxVerticesPerUploadChunk + 1,
            ViewerPrimitiveKind.Lines);

        Assert.Equal([(0, OpenGlLineRenderer.MaxVerticesPerUploadChunk)], exact);
        Assert.Equal(
            [(0, OpenGlLineRenderer.MaxVerticesPerUploadChunk), (OpenGlLineRenderer.MaxVerticesPerUploadChunk, 2)],
            plusSegment);
        Assert.Equal([(0, OpenGlLineRenderer.MaxVerticesPerUploadChunk)], oddInvalid);
    }

    [Fact]
    public void OpenGlLineRenderer_ChunksTrianglesOnTriangleBoundaries()
    {
        var chunkSize = OpenGlLineRenderer.MaxVerticesPerUploadChunk - OpenGlLineRenderer.MaxVerticesPerUploadChunk % 3;
        var exact = OpenGlLineRenderer.ChunkVertices(chunkSize, ViewerPrimitiveKind.Triangles);
        var plusTriangle = OpenGlLineRenderer.ChunkVertices(chunkSize + 3, ViewerPrimitiveKind.Triangles);
        var invalidRemainder = OpenGlLineRenderer.ChunkVertices(chunkSize + 1, ViewerPrimitiveKind.Triangles);

        Assert.Equal([(0, chunkSize)], exact);
        Assert.Equal([(0, chunkSize), (chunkSize, 3)], plusTriangle);
        Assert.Equal([(0, chunkSize)], invalidRemainder);
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
    public void ExecutedPathCache_MatchesCompletedCut()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0", "G1 X3 Y0");
        var completedLines = new HashSet<uint> { 2, 4 };

        var expected = GCodePathBuilder.BuildCompletedCut(tokens, new Point3D(), completedLines);
        var actual = GCodePathBuilder.Build(tokens, new Point3D()).ExecutedPathCache.BuildCompletedCut(completedLines);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExecutedPathCache_LargeCompletedCut_PreservesSegmentsBeyondLayerCap()
    {
        const int moves = PathDecimator.MaxVerticesPerLayer / 2 + 1;
        var blocks = BuildLinearMoveBlocks(moves);
        var completedLines = Enumerable.Range(2, moves).Select(line => (uint)line).ToHashSet();

        var points = GCodePathBuilder.Build(Parse(blocks), new Point3D())
            .ExecutedPathCache
            .BuildCompletedCut(completedLines);

        Assert.True(points.Count > PathDecimator.MaxVerticesPerLayer);
        Assert.Equal(0, points.Count % 2);
        Assert.Equal(moves * 2, points.Count);
        Assert.Equal(new NumericVector3(0f, 0f, 0f), points[0]);
        Assert.Equal(new NumericVector3(moves, 0f, 0f), points[^1]);
    }

    [Fact]
    public void ExecutedPathCache_CutSplit_SeparatesPendingAndCompletedSegments()
    {
        var tokens = Parse("G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0", "G1 X3 Y0");
        var split = GCodePathBuilder.Build(tokens, new Point3D())
            .ExecutedPathCache
            .BuildCutSplit(new HashSet<uint> { 2, 4 });

        Assert.Equal(
            [new NumericVector3(0f, 0f, 0f), new NumericVector3(1f, 0f, 0f),
             new NumericVector3(2f, 0f, 0f), new NumericVector3(3f, 0f, 0f)],
            split.Completed);
        Assert.Equal(
            [new NumericVector3(1f, 0f, 0f), new NumericVector3(2f, 0f, 0f)],
            split.Pending);
    }

    [Fact]
    public void ExecutedPathCache_LargeCutSplit_PreservesCompletedSegmentsBeyondLayerCap()
    {
        const int moves = PathDecimator.MaxVerticesPerLayer / 2 + 1;
        var blocks = BuildLinearMoveBlocks(moves);
        var completedLines = Enumerable.Range(2, moves).Select(line => (uint)line).ToHashSet();

        var split = GCodePathBuilder.Build(Parse(blocks), new Point3D())
            .ExecutedPathCache
            .BuildCutSplit(completedLines);

        Assert.Empty(split.Pending);
        Assert.True(split.Completed.Count > PathDecimator.MaxVerticesPerLayer);
        Assert.Equal(0, split.Completed.Count % 2);
        Assert.Equal(moves * 2, split.Completed.Count);
        Assert.Equal(new NumericVector3(moves, 0f, 0f), split.Completed[^1]);
    }

    [Fact]
    public void ExecutedPathAccumulator_LargeCompletedCut_AppendedMatchesRebuild()
    {
        const int moves = PathDecimator.MaxVerticesPerLayer / 2 + 1;
        var blocks = BuildLinearMoveBlocks(moves);
        var completedLines = Enumerable.Range(2, moves).Select(line => (uint)line).ToArray();
        var cache = GCodePathBuilder.Build(Parse(blocks), new Point3D()).ExecutedPathCache;
        var appended = cache.CreateAccumulator();
        var rebuilt = cache.CreateAccumulator();

        foreach (var line in completedLines)
            Assert.True(appended.AppendCompletedLine(line));
        rebuilt.Rebuild(completedLines.ToHashSet());

        var appendedPoints = appended.GetPoints();
        var rebuiltPoints = rebuilt.GetPoints();
        Assert.Equal(rebuiltPoints, appendedPoints);
        Assert.True(appendedPoints.Count > PathDecimator.MaxVerticesPerLayer);
        Assert.Equal(0, appendedPoints.Count % 2);
        Assert.Equal(new NumericVector3(moves, 0f, 0f), appendedPoints[^1]);
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
    public void ExecutedPathCache_SkippedRapidStillAdvancesInterpreterPosition()
    {
        var tokens = Parse("G21 G90 F100", "G0 X5 Y0", "G1 X6 Y0");
        var completedLines = new HashSet<uint> { 3 };

        var expected = GCodePathBuilder.BuildCompletedCut(tokens, new Point3D(), completedLines);
        var actual = GCodePathBuilder.Build(tokens, new Point3D()).ExecutedPathCache.BuildCompletedCut(completedLines);

        Assert.Equal(expected, actual);
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

    [Fact]
    public void ExecutedPathCache_UsesGeneratedProgramLineNumbers()
    {
        var original = GrblInfo.UseLinenumbers;
        SetUseLinenumbers(true);

        try
        {
            GCodeFileService.Instance.LoadFromLines(
                ["G21 G90 F100", "G1 X1 Y0", "G1 X2 Y0"],
                @"C:\tmp\viewer-line-number-cache-test.nc");

            var completedLine = GCodeFileService.Instance.Data.Single(block => block.DisplayData == "G1X1Y0").LineNum;
            var completedLines = new HashSet<uint> { completedLine };
            var expected = GCodePathBuilder.BuildCompletedCut(
                GCodeFileService.Instance.Tokens,
                new Point3D(),
                completedLines);
            var actual = GCodePathBuilder.Build(GCodeFileService.Instance.Tokens, new Point3D())
                .ExecutedPathCache
                .BuildCompletedCut(completedLines);

            Assert.Equal(20u, completedLine);
            Assert.Equal(expected, actual);
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

    static string[] BuildLinearMoveBlocks(int moves)
    {
        var blocks = new string[moves + 1];
        blocks[0] = "G21 G90 F100";
        for (var i = 1; i <= moves; i++)
            blocks[i] = $"G1 X{i} Y0";

        return blocks;
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
