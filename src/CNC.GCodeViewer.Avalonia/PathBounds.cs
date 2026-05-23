using NumericVector3 = System.Numerics.Vector3;
using CNC.Core.Geometry;

namespace CNC.GCodeViewer.Avalonia;

internal readonly struct PathBounds
{
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MinZ { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
    public double MaxZ { get; init; }
    public bool HasValue { get; init; }

    public double SizeX => MaxX - MinX;
    public double SizeY => MaxY - MinY;
    public double SizeZ => MaxZ - MinZ;
    public double MaxSize => Math.Max(Math.Max(SizeX, SizeY), SizeZ);

    public Point3D Center => new(
        (MinX + MaxX) / 2d,
        (MinY + MaxY) / 2d,
        (MinZ + MaxZ) / 2d);

    public static PathBounds FromSegments(GCodePathSegments segments)
    {
        var hasPoint = false;
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;

        void Include(IEnumerable<NumericVector3> points)
        {
            foreach (var p in points)
            {
                hasPoint = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }
        }

        Include(segments.Cut);
        Include(segments.Rapid);
        Include(segments.Retract);

        if (!hasPoint)
            return new PathBounds();

        return new PathBounds
        {
            MinX = minX,
            MinY = minY,
            MinZ = minZ,
            MaxX = maxX,
            MaxY = maxY,
            MaxZ = maxZ,
            HasValue = true
        };
    }

    public bool IsEmpty => !HasValue;
}
