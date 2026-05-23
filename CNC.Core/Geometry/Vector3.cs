namespace CNC.Core.Geometry;

public struct Vector3(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;

    public Vector3 RotateZ(double cx, double cy, double radians)
    {
        var dx = X - cx;
        var dy = Y - cy;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Vector3(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos, Z);
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
}
