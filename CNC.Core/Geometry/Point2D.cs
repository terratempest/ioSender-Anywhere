namespace CNC.Core.Geometry;

public struct Point2D(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}
