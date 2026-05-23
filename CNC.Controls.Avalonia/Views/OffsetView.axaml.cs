using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Views;

public partial class OffsetView : UserControl
{
    readonly GrblViewModel _parameters = new();
    CoordinateSystem? _selectedOffset;
    volatile bool _awaitCoord;
    Action<string>? _gotPosition;

    public static readonly StyledProperty<bool> CanEditProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(CanEdit));

    public static readonly StyledProperty<bool> IsPredefinedProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(IsPredefined));

    public OffsetView()
    {
        InitializeComponent();
        Offset = new CoordinateSystem();
        _parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
        if (!GrblInfo.IsGrblHAL)
            _parameters.PropertyChanged += Parameters_PropertyChanged;
        AttachedToVisualTree += (_, _) => ApplyLatheColumnHeaders();
    }

    public CoordinateSystem Offset { get; }
    public bool CanEdit { get => GetValue(CanEditProperty); set => SetValue(CanEditProperty, value); }
    public bool IsPredefined { get => GetValue(IsPredefinedProperty); set => SetValue(IsPredefinedProperty, value); }

    public void Activate(bool activate)
    {
        var comms = Comms.com;
        if (activate)
        {
            if (comms is { IsOpen: true })
            {
                comms.DataReceived += _parameters.DataReceived;
                GrblWorkParameters.Get(_parameters);
            }
            if (DataContext is GrblViewModel vm)
                vm.AxisEnabledFlags = GrblInfo.AxisFlags;
            dgrOffsets.ItemsSource = GrblWorkParameters.CoordinateSystems;
            dgrOffsets.SelectedIndex = 0;
        }
        else
        {
            if (comms != null)
            {
                comms.DataReceived -= _parameters.DataReceived;
                comms.PurgeQueue();
            }
            dgrOffsets.ItemsSource = null;
        }
    }

    void ApplyLatheColumnHeaders()
    {
        if (!GrblInfo.LatheModeEnabled || dgrOffsets.Columns.Count < 2)
            return;
        var header = $"X offset ({(GrblWorkParameters.LatheMode == LatheMode.Radius ? "R" : "D")})";
        // Column headers are templates in Avalonia; lathe label is applied via cvXOffset label binding at runtime if needed.
        cvXOffset.Label = header + ":";
    }

    void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.MachinePosition):
                if (!(_awaitCoord = double.IsNaN(_parameters.MachinePosition.Values[0])))
                {
                    Offset.Set(_parameters.MachinePosition);
                    _parameters.SuspendPositionNotifications = true;
                    _parameters.Clear();
                    _parameters.MachinePosition.Clear();
                    _parameters.SuspendPositionNotifications = false;
                }
                break;
            case nameof(GrblViewModel.GrblError):
                GrblWorkParameters.Get(_parameters);
                break;
        }
    }

    void dgrOffsets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is CoordinateSystem row)
        {
            _selectedOffset = row;
            IsPredefined = row.Code is "G28" or "G30";

            for (var i = 0; i < Offset.Values.Length; i++)
            {
                if (double.IsNaN(Offset.Values[i]))
                    Offset.Values[i] = 120;
                Offset.Values[i] = row.Values[i];
            }

            Offset.Code = row.Code;
            if (IsPredefined)
                btnCurrPos_Click(null, new RoutedEventArgs());
            CanEdit = !IsPredefined;
        }
        else
            _selectedOffset = null;
    }

    void saveOffset(string axis)
    {
        if (_selectedOffset == null || DataContext is not GrblViewModel model)
            return;

        var newpos = new Position(Offset);
        newpos.X = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, _selectedOffset.X);
        GrblParserState.Get();
        var mChanged = GrblParserState.IsMetric != model.IsMetric;
        var cmd = mChanged ? model.IsMetric ? "G21" : "G20" : string.Empty;
        model.Message = string.Empty;

        if (_selectedOffset.Id == 0)
        {
            var code = _selectedOffset.Code is "G28" or "G30" ? _selectedOffset.Code + ".1" : _selectedOffset.Code;
            cmd += axis == "ClearAll" || IsPredefined
                ? _selectedOffset.Code == "G43.1" ? "G49" : _selectedOffset.Code + ".1"
                : string.Format("G90{0}{1}", code, newpos.ToString(axis == "All" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis)));
        }
        else
            cmd += string.Format("G90G10L2P{0}{1}", _selectedOffset.Id, newpos.ToString(axis is "All" or "ClearAll" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis)));

        Comms.com?.WriteCommand(cmd);
        if (mChanged)
            Comms.com?.WriteCommand(model.IsMetric ? "G20" : "G21");
    }

    void cvOffset_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedOffset == null || sender is not CoordValueSetControl control)
            return;

        var axisletter = control.Tag as string ?? "X";
        var axis = GrblInfo.AxisLetterToIndex(axisletter);
        _selectedOffset.Values[axis] = Offset.Values[axis];
        saveOffset(axisletter);
    }

    void btnSetAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedOffset == null)
            return;
        for (var i = 0; i < Offset.Values.Length; i++)
            _selectedOffset.Values[i] = Offset.Values[i];
        saveOffset("All");
    }

    void btnClearAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedOffset == null)
            return;
        for (var i = 0; i < Offset.Values.Length; i++)
            Offset.Values[i] = _selectedOffset.Values[i] = 0d;
        saveOffset("ClearAll");
    }

    void RequestStatus()
    {
        _parameters.WorkPositionOffset.Z = double.NaN;
        Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));
    }

    void btnCurrPos_Click(object? sender, RoutedEventArgs e)
    {
        bool? res = null;
        var cancellationToken = new CancellationToken();
        _awaitCoord = true;
        _parameters.OnRealtimeStatusProcessed += DataReceived;

        new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => _gotPosition += a,
                a => _gotPosition = (Action<string>?)Delegate.Remove(_gotPosition, a),
                1000, RequestStatus);
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        _parameters.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(_parameters.OnRealtimeStatusProcessed, DataReceived)!;
    }

    void DataReceived(string data)
    {
        if (_awaitCoord)
        {
            Thread.Sleep(50);
            Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));
        }
        else
            _gotPosition?.Invoke("ok");
    }
}
