using CNC.Core.Geometry;

namespace CNC.Controls.Probing;

static class Vector3GCodeExtensions
{
    public static double[] ToArray(this Vector3 v) => [v.X, v.Y, v.Z];

    public static Vector3 Round(this Vector3 v, int precision) =>
        new(Math.Round(v.X, precision), Math.Round(v.Y, precision), Math.Round(v.Z, precision));

    public static double Magnitude(this Vector3 v) =>
        Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    public static Vector3 RollComponents(this Vector3 value, int turns)
    {
        var components = new[] { value.X, value.Y, value.Z };
        var roll = new double[3];
        for (var i = 0; i < 3; i++)
            roll[i] = components[(i - turns + 300) % 3];
        return new Vector3(roll[0], roll[1], roll[2]);
    }
}
