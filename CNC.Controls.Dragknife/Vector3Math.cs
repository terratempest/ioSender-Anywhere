using CNC.Core.Geometry;

namespace CNC.Controls.DragKnife;

static class Vector3Math
{
    public static double[] ToArray(this Vector3 v) => [v.X, v.Y, v.Z];

    public static double Magnitude(this Vector3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    public static Vector3 NormalizeOrDefault(this Vector3 v)
    {
        var mag = v.Magnitude();
        return mag > 0d ? new Vector3(v.X / mag, v.Y / mag, v.Z / mag) : new Vector3(1d, 0d, 0d);
    }

    public static double DotProduct(this Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static double Angle(this Vector3 a, Vector3 b)
    {
        var magA = a.Magnitude();
        var magB = b.Magnitude();
        if (magA == 0d || magB == 0d)
            return 0d;
        var cos = Math.Clamp(a.DotProduct(b) / (magA * magB), -1d, 1d);
        return Math.Acos(cos);
    }

    public static bool Equals(this Vector3 a, Vector3 b, double tolerance = 1e-9)
        => Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance && Math.Abs(a.Z - b.Z) < tolerance;

    public static Vector3 Add(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 Subtract(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 Scale(Vector3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
}
