namespace CNC.Core.Geometry;

public struct Point3D(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;

    public Point3D() : this(0, 0, 0) { }
}
