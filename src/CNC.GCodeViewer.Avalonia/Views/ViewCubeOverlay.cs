using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CNC.GCodeViewer.Avalonia.OpenGl;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.Views;

public enum ViewCubeFace
{
    Up,
    Down,
    Left,
    Right,
    Front,
    Back
}

/// <summary>Screen-space orientation cube (bottom-right), matching the machine orientation.</summary>
public sealed class ViewCubeOverlay : Control
{
    public static readonly StyledProperty<ViewerCamera?> CameraProperty =
        AvaloniaProperty.Register<ViewCubeOverlay, ViewerCamera?>(nameof(Camera));

    const double CubeScale = 27.6d;
    const double LabelSize = 0.84d;

    readonly List<FaceShape> _faces = [];

    public event EventHandler<ViewCubeFace>? FaceClicked;

    public ViewerCamera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    static readonly Color Red = Color.FromRgb(215, 70, 70);
    static readonly Color Green = Color.FromRgb(70, 185, 90);
    static readonly Color Blue = Color.FromRgb(75, 130, 225);
    static readonly IPen EdgePen = new Pen(new SolidColorBrush(Color.FromArgb(210, 235, 235, 235)), 1);
    static readonly IBrush TextBrush = Brushes.White;

    sealed record FaceDef(
        ViewCubeFace Face,
        string Label,
        NumericVector3 Normal,
        NumericVector3 U,
        NumericVector3 V,
        Color Color);

    sealed record FaceShape(ViewCubeFace Face, Point[] Points, double Depth);

    static readonly FaceDef[] FaceDefs =
    [
        new(ViewCubeFace.Up, "U", NumericVector3.UnitZ, NumericVector3.UnitX, NumericVector3.UnitY, Blue),
        new(ViewCubeFace.Down, "D", -NumericVector3.UnitZ, NumericVector3.UnitX, -NumericVector3.UnitY, Blue),
        new(ViewCubeFace.Left, "L", NumericVector3.UnitY, NumericVector3.UnitX, NumericVector3.UnitZ, Green),
        new(ViewCubeFace.Right, "R", -NumericVector3.UnitY, -NumericVector3.UnitX, NumericVector3.UnitZ, Green),
        new(ViewCubeFace.Front, "F", NumericVector3.UnitX, NumericVector3.UnitY, NumericVector3.UnitZ, Red),
        new(ViewCubeFace.Back, "B", -NumericVector3.UnitX, -NumericVector3.UnitY, NumericVector3.UnitZ, Red),
    ];

    static ViewCubeOverlay()
    {
        AffectsRender<ViewCubeOverlay>(CameraProperty);
    }

    public ViewCubeOverlay()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override Size MeasureOverride(Size availableSize) => new(110, 110);

    public override void Render(DrawingContext context)
    {
        _faces.Clear();
        var camera = Camera;
        if (camera == null || !camera.IsValid())
            return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 16 || h < 16)
            return;

        var center = new Point(w / 2d, h / 2d);

        var look = NumericVector3.Normalize(camera.LookDirection);
        var up = NumericVector3.Normalize(camera.UpDirection);
        var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));
        var toCamera = -look;

        Point Project(NumericVector3 worldDir)
        {
            var sx = NumericVector3.Dot(worldDir, right);
            var sy = NumericVector3.Dot(worldDir, up);
            return new Point(center.X + sx * CubeScale, center.Y - sy * CubeScale);
        }

        foreach (var face in FaceDefs)
        {
            var c = face.Normal * 0.5f;
            var u = face.U * 0.5f;
            var v = face.V * 0.5f;
            var points = new[]
            {
                Project(c - u - v),
                Project(c + u - v),
                Project(c + u + v),
                Project(c - u + v),
            };
            _faces.Add(new FaceShape(face.Face, points, NumericVector3.Dot(face.Normal, toCamera)));
        }

        _faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var shape in _faces)
            DrawFace(context, shape);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        for (var i = _faces.Count - 1; i >= 0; i--)
        {
            var shape = _faces[i];
            if (!Contains(shape.Points, p))
                continue;

            FaceClicked?.Invoke(this, shape.Face);
            e.Handled = true;
            return;
        }
    }

    static void DrawFace(DrawingContext context, FaceShape shape)
    {
        var def = FaceDefs.First(f => f.Face == shape.Face);
        var color = def.Color;
        var visible = shape.Depth >= 0d;
        var fill = new SolidColorBrush(Color.FromArgb(visible ? (byte)215 : (byte)85, color.R, color.G, color.B));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(shape.Points[0], true);
            ctx.LineTo(shape.Points[1]);
            ctx.LineTo(shape.Points[2]);
            ctx.LineTo(shape.Points[3]);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(fill, EdgePen, geometry);

        if (!visible)
            return;

        DrawFaceLabel(context, shape, def.Label);
    }

    static void DrawFaceLabel(DrawingContext context, FaceShape shape, string label)
    {
        var typeface = new Typeface(FontFamily.Default);
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            LabelSize,
            TextBrush);

        var x = shape.Points[0] - shape.Points[1];
        var y = shape.Points[0] - shape.Points[3];
        if (shape.Face is ViewCubeFace.Front or ViewCubeFace.Back)
        {
            x = -x;
        }
        var inset = 0.23d;
        var origin = shape.Face is ViewCubeFace.Front or ViewCubeFace.Back
            ? shape.Points[3] + x * inset + y * inset
            : shape.Points[2] + x * inset + y * inset;
        var widthScale = 1d - inset * 2d;
        var heightScale = 1d - inset * 2d;
        var matrix = new Matrix(
            x.X * widthScale,
            x.Y * widthScale,
            y.X * heightScale,
            y.Y * heightScale,
            origin.X,
            origin.Y);

        using (context.PushTransform(matrix))
            context.DrawText(text, new Point((1d - text.Width) / 2d, (1d - text.Height) / 2d));
    }

    static bool Contains(Point[] poly, Point point)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > point.Y) == (poly[j].Y > point.Y))
                continue;

            var x = (poly[j].X - poly[i].X) * (point.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X;
            if (point.X < x)
                inside = !inside;
        }

        return inside;
    }
}
