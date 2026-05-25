using NumericVector3 = System.Numerics.Vector3;
using CNC.Core;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerEnvelopeBuilder
{
    public static List<NumericVector3> JobBox(PathBounds bounds)
    {
        if (bounds.IsEmpty)
            return [];

        const double minExtent = 0.001;
        var sizeY = Math.Max(bounds.SizeY, minExtent);
        return WireBox(
            bounds.MinX,
            bounds.MinY,
            bounds.MinZ,
            bounds.SizeX,
            sizeY,
            bounds.SizeZ);
    }

    public static List<NumericVector3> WorkAreaBox(GrblViewModel? grbl)
    {
        if (!GrblInfo.HomingEnabled)
            return [];

        var x = GrblInfo.MaxTravel.X;
        var y = GrblInfo.MaxTravel.Y;
        var z = GrblInfo.MaxTravel.Z;
        if (x <= 0d || z <= 0d)
            return [];

        var ox = grbl?.WorkPositionOffset.X ?? 0d;
        var oy = grbl?.WorkPositionOffset.Y ?? 0d;
        var oz = grbl?.WorkPositionOffset.Z ?? 0d;

        var sizeY = Math.Max(y, 0.001);
        if (GrblInfo.ForceSetOrigin)
            return WireBox(-ox, -oy, -oz - z, x, sizeY, z);

        return WireBox(-x - ox, -y - oy, -z - oz, x, sizeY, z);
    }

    static List<NumericVector3> WireBox(double x, double y, double z, double sx, double sy, double sz)
    {
        var maxX = x + sx;
        var maxY = y + sy;
        var maxZ = z + sz;

        NumericVector3 P(double px, double py, double pz) => new((float)px, (float)py, (float)pz);

        return
        [
            P(x, y, z), P(maxX, y, z),
            P(maxX, y, z), P(maxX, maxY, z),
            P(maxX, maxY, z), P(x, maxY, z),
            P(x, maxY, z), P(x, y, z),

            P(x, y, maxZ), P(maxX, y, maxZ),
            P(maxX, y, maxZ), P(maxX, maxY, maxZ),
            P(maxX, maxY, maxZ), P(x, maxY, maxZ),
            P(x, maxY, maxZ), P(x, y, maxZ),

            P(x, y, z), P(x, y, maxZ),
            P(maxX, y, z), P(maxX, y, maxZ),
            P(maxX, maxY, z), P(maxX, maxY, maxZ),
            P(x, maxY, z), P(x, maxY, maxZ)
        ];
    }
}
