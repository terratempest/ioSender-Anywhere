using System.Globalization;
using System.Xml;
using CNC.Core.Geometry;

namespace CNC.Controls.Probing;

public static class HeightMapConstants
{
    public static NumberFormatInfo DecimalParse { get; } = new() { NumberDecimalSeparator = "." };

    public static NumberFormatInfo DecimalOutput { get; } = new()
    {
        NumberDecimalSeparator = ".",
        NumberDecimalDigits = 3
    };
}

/// <summary>Height probe grid data (OpenCNCPilot-derived; 3D preview left to UI).</summary>
public class HeightMap
{
    public double?[,] Points { get; private set; } = null!;
    public int SizeX { get; private set; }
    public int SizeY { get; private set; }
    public List<Tuple<int, int>> NotProbed { get; } = [];
    public Vector2 Min { get; private set; }
    public Vector2 Max { get; private set; }
    public double MinHeight { get; private set; } = double.MaxValue;
    public double MaxHeight { get; private set; } = double.MinValue;
    public double ZOffset { get; set; }

    public int TotalPoints => SizeX * SizeY;
    public Vector2 Delta => Max - Min;
    public double GridX => (Max.X - Min.X) / (SizeX - 1);
    public double GridY => (Max.Y - Min.Y) / (SizeY - 1);

    public event Action? MapUpdated;

    public HeightMap(double gridSizeX, double gridSizeY, Vector2 min, Vector2 max) =>
        CreateHeightMap(gridSizeX, gridSizeY, min, max);

    public HeightMap(double gridSize, Vector2 min, Vector2 max) =>
        CreateHeightMap(gridSize, gridSize, min, max);

    HeightMap()
    {
    }

    void CreateHeightMap(double gridSizeX, double gridSizeY, Vector2 min, Vector2 max)
    {
        if (min.X == max.X || min.Y == max.Y)
            throw new Exception(ProbingStrings.HeightMapNarrow);

        var pointsX = (int)Math.Ceiling((max.X - min.X) / gridSizeX) + 1;
        var pointsY = (int)Math.Ceiling((max.Y - min.Y) / gridSizeY) + 1;

        if (pointsX < 2 || pointsY < 2)
            throw new Exception(ProbingStrings.HeightMapMinSize);

        Points = new double?[pointsX, pointsY];

        if (max.X < min.X)
            (min.X, max.X) = (max.X, min.X);

        if (max.Y < min.Y)
            (min.Y, max.Y) = (max.Y, min.Y);

        Min = min;
        Max = max;
        SizeX = pointsX;
        SizeY = pointsY;

        for (var x = 0; x < SizeX; x++)
        {
            for (var y = 0; y < SizeY; y++)
                NotProbed.Add(new Tuple<int, int>(x, y));
        }
    }

    public double InterpolateZ(double x, double y)
    {
        if (x > Max.X || x < Min.X || y > Max.Y || y < Min.Y)
            return MaxHeight;

        x -= Min.X;
        y -= Min.Y;
        x /= GridX;
        y /= GridY;

        var iLx = (int)Math.Floor(x);
        var iLy = (int)Math.Floor(y);
        var iHx = (int)Math.Ceiling(x);
        var iHy = (int)Math.Ceiling(y);
        var fX = x - iLx;
        var fY = y - iLy;

        var linUpper = Points[iHx, iHy]!.Value * fX + Points[iLx, iHy]!.Value * (1 - fX);
        var linLower = Points[iHx, iLy]!.Value * fX + Points[iLx, iLy]!.Value * (1 - fX);

        return linUpper * fY + linLower * (1 - fY) + ZOffset;
    }

    public Vector2 GetCoordinates(int x, int y) =>
        new(x * (Delta.X / (SizeX - 1)) + Min.X, y * (Delta.Y / (SizeY - 1)) + Min.Y);

    public void AddPoint(int x, int y, double height)
    {
        Points[x, y] = height;

        if (height > MaxHeight)
            MaxHeight = height;
        if (height < MinHeight)
            MinHeight = height;

        MapUpdated?.Invoke();
    }

    public static HeightMap Load(string path)
    {
        var map = new HeightMap();

        using var r = XmlReader.Create(path);
        while (r.Read())
        {
            if (!r.IsStartElement())
                continue;

            switch (r.Name)
            {
                case "heightmap":
                    map.Min = new Vector2(
                        double.Parse(r["MinX"]!, HeightMapConstants.DecimalParse),
                        double.Parse(r["MinY"]!, HeightMapConstants.DecimalParse));
                    map.Max = new Vector2(
                        double.Parse(r["MaxX"]!, HeightMapConstants.DecimalParse),
                        double.Parse(r["MaxY"]!, HeightMapConstants.DecimalParse));
                    map.SizeX = int.Parse(r["SizeX"]!);
                    map.SizeY = int.Parse(r["SizeY"]!);
                    map.Points = new double?[map.SizeX, map.SizeY];
                    break;

                case "point":
                {
                    var x = int.Parse(r["X"]!);
                    var y = int.Parse(r["Y"]!);
                    var height = double.Parse(r.ReadInnerXml(), HeightMapConstants.DecimalParse);
                    map.Points[x, y] = height;
                    if (height > map.MaxHeight)
                        map.MaxHeight = height;
                    if (height < map.MinHeight)
                        map.MinHeight = height;
                    break;
                }
            }
        }

        for (var x = 0; x < map.SizeX; x++)
        {
            for (var y = 0; y < map.SizeY; y++)
            {
                if (!map.Points[x, y].HasValue)
                    map.NotProbed.Add(new Tuple<int, int>(x, y));
            }
        }

        return map;
    }

    public void Save(string path)
    {
        var set = new XmlWriterSettings { Indent = true };
        using var w = XmlWriter.Create(path, set);
        w.WriteStartDocument();
        w.WriteStartElement("heightmap");
        w.WriteAttributeString("MinX", Min.X.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("MinY", Min.Y.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("MaxX", Max.X.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("MaxY", Max.Y.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("SizeX", SizeX.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("SizeY", SizeY.ToString(HeightMapConstants.DecimalOutput));
        w.WriteAttributeString("ZOffset", ZOffset.ToString(HeightMapConstants.DecimalOutput));

        for (var x = 0; x < SizeX; x++)
        {
            for (var y = 0; y < SizeY; y++)
            {
                if (!Points[x, y].HasValue)
                    continue;

                w.WriteStartElement("point");
                w.WriteAttributeString("X", x.ToString());
                w.WriteAttributeString("Y", y.ToString());
                w.WriteString(Points[x, y]!.Value.ToString(HeightMapConstants.DecimalOutput));
                w.WriteEndElement();
            }
        }

        w.WriteEndElement();
    }

    public HeightMapPreview BuildPreview()
    {
        var grid = new Point3D[SizeX * SizeY];
        var idx = 0;
        for (var x = 0; x < SizeX; x++)
        {
            for (var y = 0; y < SizeY; y++)
                grid[idx++] = new Point3D(Min.X + x * Delta.X / (SizeX - 1), Min.Y + y * Delta.Y / (SizeY - 1), 0);
        }

        return new HeightMapPreview
        {
            SizeX = SizeX,
            SizeY = SizeY,
            GridPoints = grid,
            Boundary =
            [
                new Point3D(Min.X, Min.Y, 0),
                new Point3D(Min.X, Max.Y, 0),
                new Point3D(Max.X, Max.Y, 0),
                new Point3D(Max.X, Min.Y, 0),
                new Point3D(Min.X, Min.Y, 0)
            ]
        };
    }

    public static HeightMapPreview BuildPreview(Vector2 min, Vector2 max, double gridSize)
    {
        var minTemp = new Vector2(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y));
        var maxTemp = new Vector2(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y));
        min = minTemp;
        max = maxTemp;

        if (max.X - min.X == 0 || max.Y - min.Y == 0)
            return new HeightMapPreview();

        var pointsX = (int)Math.Ceiling((max.X - min.X) / gridSize) + 1;
        var pointsY = (int)Math.Ceiling((max.Y - min.Y) / gridSize) + 1;
        return BuildPreview(min, max, pointsX, pointsY);
    }

    public static HeightMapPreview BuildPreview(Vector2 min, Vector2 max, int pointsX, int pointsY)
    {
        var minTemp = new Vector2(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y));
        var maxTemp = new Vector2(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y));
        min = minTemp;
        max = maxTemp;

        var gridX = (max.X - min.X) / (pointsX - 1);
        var gridY = (max.Y - min.Y) / (pointsY - 1);
        var grid = new Point3D[pointsX * pointsY];
        var idx = 0;

        for (var x = 0; x < pointsX; x++)
        {
            for (var y = 0; y < pointsY; y++)
                grid[idx++] = new Point3D(min.X + x * gridX, min.Y + y * gridY, 0);
        }

        return new HeightMapPreview
        {
            SizeX = pointsX,
            SizeY = pointsY,
            GridPoints = grid,
            Boundary =
            [
                new Point3D(min.X, min.Y, 0),
                new Point3D(min.X, max.Y, 0),
                new Point3D(max.X, max.Y, 0),
                new Point3D(max.X, min.Y, 0),
                new Point3D(min.X, min.Y, 0)
            ]
        };
    }
}

public struct Vector2(double x, double y) : IEquatable<Vector2>
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;

    public static Vector2 operator +(Vector2 v1, Vector2 v2) => new(v1.X + v2.X, v1.Y + v2.Y);
    public static Vector2 operator -(Vector2 v1, Vector2 v2) => new(v1.X - v2.X, v1.Y - v2.Y);
    public static Vector2 operator *(Vector2 v1, double s2) => new(v1.X * s2, v1.Y * s2);
    public static Vector2 operator *(double s1, Vector2 v2) => v2 * s1;
    public static Vector2 operator /(Vector2 v1, double s2) => new(v1.X / s2, v1.Y / s2);
    public static Vector2 operator -(Vector2 v1) => new(-v1.X, -v1.Y);

    public static bool operator ==(Vector2 v1, Vector2 v2) =>
        Math.Abs(v1.X - v2.X) <= EqualityTolerance && Math.Abs(v1.Y - v2.Y) <= EqualityTolerance;

    public static bool operator !=(Vector2 v1, Vector2 v2) => !(v1 == v2);

    public bool Equals(Vector2 other) => other == this;
    public override bool Equals(object? other) => other is Vector2 v && v == this;
    public override int GetHashCode() => (int)((X + Y) % int.MaxValue);

    public const double EqualityTolerance = double.Epsilon;
}

public sealed class HeightMapPreview
{
    public int SizeX { get; init; }
    public int SizeY { get; init; }
    public Point3D[] GridPoints { get; init; } = [];
    public Point3D[] Boundary { get; init; } = [];
}
