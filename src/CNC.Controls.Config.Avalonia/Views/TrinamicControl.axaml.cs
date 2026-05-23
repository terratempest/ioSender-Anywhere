using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Config;

public partial class TrinamicControl : UserControl, IGrblConfigTab
{
    readonly EnumFlags<AxisFlags> _axisEnabled = new(AxisFlags.X);
    readonly List<Line> _lines = [];
    readonly List<Line> _lines2 = [];

    GrblViewModel? _model;
    int _sgIndex;
    int _yScale = 4;
    bool _readStatus;
    bool _grblReset;
    AxisFlags _selectedAxis = AxisFlags.X;

    public static readonly StyledProperty<bool> SFiltEnabledProperty =
        AvaloniaProperty.Register<TrinamicControl, bool>(nameof(SFiltEnabled));

    public static readonly StyledProperty<string> DriverStatusProperty =
        AvaloniaProperty.Register<TrinamicControl, string>(nameof(DriverStatus), string.Empty);

    public static readonly StyledProperty<int> SGValueProperty =
        AvaloniaProperty.Register<TrinamicControl, int>(nameof(SGValue));

    public static readonly StyledProperty<int> SGValueMinProperty =
        AvaloniaProperty.Register<TrinamicControl, int>(nameof(SGValueMin), -64);

    public static readonly StyledProperty<int> SGValueMaxProperty =
        AvaloniaProperty.Register<TrinamicControl, int>(nameof(SGValueMax), 63);

    static readonly Action<string> NoopResponseHandler = _ => { };

    static TrinamicControl()
    {
        SFiltEnabledProperty.Changed.AddClassHandler<TrinamicControl>((_, e) =>
        {
            if (e.NewValue is bool enabled && Comms.com is { IsOpen: true } comms)
                comms.WriteCommand($"M122H{(enabled ? 1 : 0)}");
        });
    }

    public TrinamicControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => CaptureModel();
    }

    public GrblConfigType GrblConfigType => GrblConfigType.Trinamic;

    public EnumFlags<AxisFlags> AxisEnabled => _axisEnabled;

    public bool SFiltEnabled
    {
        get => GetValue(SFiltEnabledProperty);
        set => SetValue(SFiltEnabledProperty, value);
    }

    public string DriverStatus
    {
        get => GetValue(DriverStatusProperty);
        set => SetValue(DriverStatusProperty, value);
    }

    public int SGValue
    {
        get => GetValue(SGValueProperty);
        set => SetValue(SGValueProperty, value);
    }

    public int SGValueMin
    {
        get => GetValue(SGValueMinProperty);
        set => SetValue(SGValueMinProperty, value);
    }

    public int SGValueMax
    {
        get => GetValue(SGValueMaxProperty);
        set => SetValue(SGValueMaxProperty, value);
    }

    public void Activate(bool activate)
    {
        if (!CaptureModel() || _model is not { } model)
            return;

        if (Comms.com is { IsOpen: true } comms)
            comms.WriteString($"M122S{(activate ? 1 : 0)}H{(SFiltEnabled ? 1 : 0)}\r");

        if (activate)
        {
            model.OnResponseReceived += ProcessSgValue;
            model.PropertyChanged += OnModelPropertyChanged;
            RefreshSgFromSettings(_selectedAxis);
            _grblReset = false;
        }
        else
        {
            RemoveResponseHandler(model, ProcessSgValue);
            model.PropertyChanged -= OnModelPropertyChanged;
        }

        var poll = ControlsPlatformContext.AppConfig?.Base.PollInterval ?? 200;
        model.Poller.SetState(activate ? poll : 0);
    }

    void OnLoaded(object? sender, RoutedEventArgs e) => CaptureModel();

    bool CaptureModel()
    {
        if (_model != null)
            return true;

        if (base.DataContext is GrblViewModel vm)
        {
            _model = vm;
            mdiControl.DataContext = vm;
            return true;
        }

        return false;
    }

    void OnAxisChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true } rb)
            return;

        var axis = rb.Name switch
        {
            "rbY" => AxisFlags.Y,
            "rbZ" => AxisFlags.Z,
            "rbA" => AxisFlags.A,
            "rbB" => AxisFlags.B,
            "rbC" => AxisFlags.C,
            _ => AxisFlags.X
        };

        _selectedAxis = axis;
        _axisEnabled.Value = axis;
        OnAxisChanged(axis);
    }

    void OnAxisChanged(AxisFlags axis)
    {
        if (Comms.com is not { IsOpen: true } comms)
            return;

        comms.WriteString($"M122{axis}S1\r");
        RefreshSgFromSettings(axis);
    }

    void RefreshSgFromSettings(AxisFlags axis)
    {
        var details = GrblSettings.Get(grblHALSetting.StallGuardBase + GrblInfo.AxisLetterToIndex(axis.ToString()));
        if (details?.Value is not { } value)
            return;

        SGValue = int.Parse(value);
        SGValueMin = (int)details.Min;
        SGValueMax = (int)details.Max;
        _yScale = SGValueMax == 255 ? 2 : 4;
    }

    void OnGetStateClick(object? sender, RoutedEventArgs e) => GetDriverStatus(_selectedAxis.ToString());

    void OnGetStateAllClick(object? sender, RoutedEventArgs e) => GetDriverStatus(string.Empty);

    void OnSgSliderReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Comms.com is { IsOpen: true } comms)
            comms.WriteString($"M914{_selectedAxis}{SGValue}\r");
    }

    void OnConfigureSgClick(object? sender, RoutedEventArgs e)
    {
        if (Comms.com is { IsOpen: true } comms)
            comms.WriteString($"${(int)(grblHALSetting.StallGuardBase + GrblInfo.AxisLetterToIndex(_selectedAxis.ToString()))}={SGValue}\r");
    }

    void GetDriverStatus(string axis)
    {
        if (_model is not { } model || Comms.com is not { } comms)
            return;

        bool? res = null;
        var cancellationToken = new CancellationToken();
        DriverStatus = string.Empty;
        comms.PurgeQueue();

        var poll = ControlsPlatformContext.AppConfig?.Base.PollInterval ?? 200;
        model.Poller.SetState(0);
        model.SuspendProcessing = true;

        new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                cancellationToken,
                ProcessStatus,
                a => model.OnResponseReceived += a,
                a => RemoveResponseHandler(model, a),
                800,
                () => comms.WriteCommand("M122" + axis));
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        model.SuspendProcessing = false;
        model.Poller.SetState(poll);
    }

    void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel vm)
            return;

        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.MDI):
                if (!vm.MDI.ToUpperInvariant().StartsWith('M'))
                {
                    PlotGrid();
                    _sgIndex = 0;
                    _lines.Clear();
                    _lines2.Clear();
                }
                break;
            case nameof(GrblViewModel.GrblReset):
                _grblReset = true;
                break;
            case nameof(GrblViewModel.GrblState):
                if (_grblReset && vm.GrblState.State == GrblStates.Idle &&
                    Comms.com is { } comms)
                {
                    comms.WriteString($"M122S1H{(SFiltEnabled ? 1 : 0)}\r");
                    _grblReset = false;
                }
                break;
            case nameof(GrblViewModel.Position):
                UpdatePositionText(vm);
                break;
        }
    }

    void UpdatePositionText(GrblViewModel vm)
    {
        txtPosition.Text = vm.IsMachinePosition
            ? $"MPos: {vm.MachinePosition}"
            : $"WPos: {vm.WorkPosition}";
    }

    void ProcessStatus(string data)
    {
        if (data == "[TRINAMIC]")
            _readStatus = true;
        else if (data == "ok")
            _readStatus = false;
        else if (_readStatus)
            UiThread.Post(() => DriverStatus += data + "\r\n");
    }

    void PlotGrid()
    {
        var height = SgPlot.Bounds.Height > 0 ? SgPlot.Bounds.Height : 240d;
        var width = SgPlot.Bounds.Width > 0 ? SgPlot.Bounds.Width : 512d;
        SgPlot.Children.Clear();

        SgPlot.Children.Add(new Line
        {
            StartPoint = new Point(0, height / 2),
            EndPoint = new Point(width, height / 2),
            Stroke = Brushes.LightGray,
            StrokeThickness = 0.5,
            StrokeDashArray = [2, 2]
        });

        var yDelta = height / 10;
        var yPos = height - yDelta;
        while (yPos > 0)
        {
            SgPlot.Children.Add(new Line
            {
                StartPoint = new Point(0, yPos),
                EndPoint = new Point(width, yPos),
                Stroke = Brushes.Gray,
                StrokeThickness = 0.5,
                StrokeDashArray = [2, 2]
            });
            yPos -= yDelta;
        }
    }

    void ProcessSgValue(string data)
    {
        if (!data.StartsWith("[SG:", StringComparison.Ordinal))
            return;

        var sep = data.IndexOf(':');
        var end = data.IndexOf(']');
        if (end <= sep)
            return;

        var payload = data[(sep + 1)..end];
        var parts = payload.Split(',');
        var v1 = int.Parse(parts[0]);
        var v2 = parts.Length == 2 ? int.Parse(parts[1]) : -1;
        UiThread.Post(() => PlotSgValue(v1, v2));
    }

    void PlotSgValue(int value, int value2)
    {
        value /= _yScale;
        var width = (int)(SgPlot.Bounds.Width > 0 ? SgPlot.Bounds.Width : 512);

        if (_lines.Count != width)
        {
            var prevY = _sgIndex == 0 ? value : _lines[_sgIndex - 1].EndPoint.Y;
            _lines.Add(new Line
            {
                StartPoint = new Point(_sgIndex == 0 ? 0 : _sgIndex - 1, prevY),
                EndPoint = new Point(_sgIndex, value),
                Stroke = Brushes.Blue
            });

            if (value2 >= 0)
            {
                value2 /= _yScale;
                var prevY2 = _sgIndex == 0 ? value2 : _lines2[_sgIndex - 1].EndPoint.Y;
                _lines2.Add(new Line
                {
                    StartPoint = new Point(_sgIndex == 0 ? 0 : _sgIndex - 1, prevY2),
                    EndPoint = new Point(_sgIndex, value2),
                    Stroke = Brushes.Green
                });
                SgPlot.Children.Add(_lines2[_sgIndex]);
            }

            SgPlot.Children.Add(_lines[_sgIndex]);
        }
        else
        {
            _sgIndex %= width;
            var prevY = _sgIndex == 0 ? value : _lines[_sgIndex - 1].EndPoint.Y;
            _lines[_sgIndex].StartPoint = new Point(_sgIndex == 0 ? 0 : _sgIndex - 1, prevY);
            _lines[_sgIndex].EndPoint = new Point(_sgIndex, value);

            if (value2 >= 0 && _lines2.Count == width)
            {
                value2 /= _yScale;
                var prevY2 = _sgIndex == 0 ? value2 : _lines2[_sgIndex - 1].EndPoint.Y;
                _lines2[_sgIndex].StartPoint = new Point(_sgIndex == 0 ? 0 : _sgIndex - 1, prevY2);
                _lines2[_sgIndex].EndPoint = new Point(_sgIndex, value2);
            }
        }

        _sgIndex++;
    }

    static void RemoveResponseHandler(GrblViewModel model, Action<string> handler)
    {
        model.OnResponseReceived = (Action<string>?)Delegate.Remove(model.OnResponseReceived, handler) ?? NoopResponseHandler;
    }
}
