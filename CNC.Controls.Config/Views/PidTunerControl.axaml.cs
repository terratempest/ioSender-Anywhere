using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using CNC.Core;

namespace CNC.Controls.Config;

public partial class PidTunerControl : UserControl, IGrblConfigTab
{
    readonly PidLogViewModel _vm = new();
    double _errorScale = 2000d;

    public PidTunerControl()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.ErrorScale = 3;
        _errorScale = _vm.ScaleFactors[3];
    }

    public GrblConfigType GrblConfigType => GrblConfigType.PidTuning;

    public void Activate(bool activate)
    {
    }

    void OnGetPidDataClick(object? sender, RoutedEventArgs e)
    {
        btnGetPidData.IsEnabled = false;
        try
        {
            GrblPidData.Load();
            PlotData();
        }
        finally
        {
            btnGetPidData.IsEnabled = true;
        }
    }

    void OnErrorScaleChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PidLogViewModel vm)
            return;

        _errorScale = vm.ScaleFactors[vm.ErrorScale];
        if (GrblPidData.Data.Rows.Count > 0)
            PlotData();
    }

    void PlotData()
    {
        var center = PidPlot.Bounds.Height > 0 ? PidPlot.Bounds.Height / 2d : 215d;
        var width = PidPlot.Bounds.Width > 0 ? PidPlot.Bounds.Width : 640d;
        var a = new Point(0, center);
        var b = new Point(0, center);
        var c = new Point(0, center);
        var d = new Point(0, center);
        var g = new Point(0, center);
        var h = new Point(0, center);

        PidPlot.Children.Clear();
        PidPlot.Children.Add(new Line
        {
            StartPoint = new Point(0, center),
            EndPoint = new Point(width, center),
            Stroke = Brushes.LightGray,
            StrokeThickness = 0.5,
            StrokeDashArray = [2, 2]
        });

        if (GrblPidData.Data.Rows.Count == 0)
            return;

        var xStep = width / GrblPidData.Data.Rows.Count;
        var xPos = xStep;

        foreach (System.Data.DataRow sample in GrblPidData.Data.Rows)
        {
            b = new Point(b.X, center - (int)((double)sample["Target"]! * 5.0));
            PidPlot.Children.Add(MakeSegment(a, b, Brushes.Green));
            a = b;
            b = new Point(Math.Floor(xPos), b.Y);

            d = new Point(d.X, center - (int)((double)sample["Actual"]! * 5.0));
            PidPlot.Children.Add(MakeSegment(c, d, Brushes.Blue));
            c = d;
            d = new Point(Math.Floor(xPos), d.Y);

            h = new Point(h.X, center - (int)((double)sample["Error"]! * _errorScale));
            PidPlot.Children.Add(MakeSegment(g, h, Brushes.Red));
            g = h;
            h = new Point(Math.Floor(xPos), h.Y);

            xPos += xStep;
        }
    }

    static Line MakeSegment(Point from, Point to, IBrush stroke) => new()
    {
        StartPoint = from,
        EndPoint = to,
        Stroke = stroke,
        StrokeThickness = 1
    };
}
