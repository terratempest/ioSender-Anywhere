using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Reduces polyline density for GPU preview (full precision stays in the program list).</summary>
internal static class PathDecimator
{
    public const int MaxVerticesPerLayer = 250_000;

    public static List<NumericVector3> Decimate(IReadOnlyList<NumericVector3> points, int maxVertices = MaxVerticesPerLayer)
    {
        if (points.Count <= maxVertices)
            return points is List<NumericVector3> list ? list : [.. points];

        var result = new List<NumericVector3>(maxVertices + 2);
        var stride = (double)(points.Count - 1) / (maxVertices - 1);

        for (var i = 0; i < maxVertices - 1; i++)
        {
            var idx = (int)Math.Round(i * stride);
            if (idx >= points.Count)
                idx = points.Count - 1;
            result.Add(points[idx]);
        }

        result.Add(points[^1]);
        return result;
    }

    public static List<NumericVector3> DecimateSegmentPairs(
        IReadOnlyList<NumericVector3> points,
        int maxVertices = MaxVerticesPerLayer)
    {
        if (points.Count <= maxVertices)
            return points is List<NumericVector3> list ? list : [.. points];

        var segmentCount = points.Count / 2;
        var maxSegments = Math.Max(1, maxVertices / 2);
        if (segmentCount <= maxSegments)
            return points is List<NumericVector3> list ? list : [.. points];

        var result = new List<NumericVector3>(maxSegments * 2);
        var stride = (double)(segmentCount - 1) / (maxSegments - 1);
        var lastSegment = -1;

        for (var i = 0; i < maxSegments; i++)
        {
            var segment = (int)Math.Round(i * stride);
            if (segment <= lastSegment)
                segment = lastSegment + 1;
            if (segment >= segmentCount)
                segment = segmentCount - 1;

            var index = segment * 2;
            result.Add(points[index]);
            result.Add(points[index + 1]);
            lastSegment = segment;
        }

        return result;
    }
}
