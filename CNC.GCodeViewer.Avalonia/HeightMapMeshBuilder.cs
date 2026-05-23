using CNC.Core.Geometry;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia;

public static class HeightMapMeshBuilder
{
    const float BoundaryThickness = 1.25f;
    const float GridThickness = 0.5f;
    const float MarkerThickness = 1f;

    public static IReadOnlyList<NumericVector3> BuildBoundaryLines(Point3D[] boundary)
    {
        if (boundary.Length < 2)
            return [];

        var lines = new List<NumericVector3>(boundary.Length * 2);
        for (var i = 0; i + 1 < boundary.Length; i++)
        {
            lines.Add(ToVector(boundary[i]));
            lines.Add(ToVector(boundary[i + 1]));
        }

        return lines;
    }

    public static IReadOnlyList<NumericVector3> BuildGridLines(Point3D[] gridPoints, int sizeX, int sizeY)
    {
        if (gridPoints.Length == 0 || sizeX < 2 || sizeY < 2)
            return [];

        var lines = new List<NumericVector3>();

        for (var x = 0; x < sizeX; x++)
        {
            for (var y = 0; y < sizeY - 1; y++)
            {
                var a = x * sizeY + y;
                var b = a + 1;
                lines.Add(ToVector(gridPoints[a]));
                lines.Add(ToVector(gridPoints[b]));
            }
        }

        for (var y = 0; y < sizeY; y++)
        {
            for (var x = 0; x < sizeX - 1; x++)
            {
                var a = x * sizeY + y;
                var b = a + sizeY;
                lines.Add(ToVector(gridPoints[a]));
                lines.Add(ToVector(gridPoints[b]));
            }
        }

        return lines;
    }

    public static IReadOnlyList<NumericVector3> BuildPointMarkers(Point3D[] gridPoints, double markerSize = 0.4)
    {
        if (gridPoints.Length == 0)
            return [];

        var half = markerSize * 0.5f;
        var lines = new List<NumericVector3>(gridPoints.Length * 4);

        foreach (var p in gridPoints)
        {
            var c = ToVector(p);
            lines.Add(new NumericVector3((float)(p.X - half), (float)p.Y, (float)p.Z));
            lines.Add(new NumericVector3((float)(p.X + half), (float)p.Y, (float)p.Z));
            lines.Add(new NumericVector3((float)p.X, (float)(p.Y - half), (float)p.Z));
            lines.Add(new NumericVector3((float)p.X, (float)(p.Y + half), (float)p.Z));
        }

        return lines;
    }

    public static IReadOnlyList<NumericVector3> BuildSurfaceWireframe(HeightMapSurfaceData data)
    {
        if (data.SizeX < 2 || data.SizeY < 2)
            return [];

        var lines = new List<NumericVector3>();

        void AddEdge(NumericVector3 a, NumericVector3 b)
        {
            lines.Add(a);
            lines.Add(b);
        }

        for (var x = 0; x < data.SizeX - 1; x++)
        {
            for (var y = 0; y < data.SizeY - 1; y++)
            {
                if (!data.Points[x, y].HasValue ||
                    !data.Points[x, y + 1].HasValue ||
                    !data.Points[x + 1, y].HasValue ||
                    !data.Points[x + 1, y + 1].HasValue)
                    continue;

                var z00 = (float)data.Points[x, y]!.Value;
                var z01 = (float)data.Points[x, y + 1]!.Value;
                var z10 = (float)data.Points[x + 1, y]!.Value;
                var z11 = (float)data.Points[x + 1, y + 1]!.Value;

                var px1 = (float)(data.MinX + (x + 1) * data.DeltaX / (data.SizeX - 1));
                var px0 = (float)(data.MinX + x * data.DeltaX / (data.SizeX - 1));
                var py1 = (float)(data.MinY + (y + 1) * data.DeltaY / (data.SizeY - 1));
                var py0 = (float)(data.MinY + y * data.DeltaY / (data.SizeY - 1));

                var p00 = new NumericVector3(px0, py0, z00);
                var p01 = new NumericVector3(px0, py1, z01);
                var p10 = new NumericVector3(px1, py0, z10);
                var p11 = new NumericVector3(px1, py1, z11);

                AddEdge(p00, p10);
                AddEdge(p10, p11);
                AddEdge(p11, p01);
                AddEdge(p01, p00);
            }
        }

        return lines;
    }

    static NumericVector3 ToVector(Point3D p) => new((float)p.X, (float)p.Y, (float)p.Z);
}
