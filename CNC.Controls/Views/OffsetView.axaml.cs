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
    readonly object _currentPositionLock = new();
    Task<bool>? _currentPositionRequest;

    public static readonly StyledProperty<bool> CanEditProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(CanEdit));

    public static readonly StyledProperty<bool> IsPredefinedProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(IsPredefined));

    public OffsetView()
    {
        InitializeComponent();
        _parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
        if (!GrblInfo.IsGrblHAL)
            _parameters.PropertyChanged += Parameters_PropertyChanged;
        AttachedToVisualTree += (_, _) => ApplyLatheColumnHeaders();
    }

    public CoordinateSystem Offset { get; } = new();
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
        dgrOffsets.Columns[1].Header = header;
        cvXOffset.Label = header + ":";
    }

    void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
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

    bool TryCaptureMachinePosition()
    {
        if (double.IsNaN(_parameters.MachinePosition.Values[0]))
            return false;

        Offset.Set(_parameters.MachinePosition);
        _parameters.SuspendPositionNotifications = true;
        _parameters.Clear();
        _parameters.MachinePosition.Clear();
        _parameters.SuspendPositionNotifications = false;
        return true;
    }

    void btnCurrPos_Click(object? sender, RoutedEventArgs e)
    {
        _ = RequestCurrentPositionAsync();
    }

    internal Task<bool> RequestCurrentPositionAsync(int timeoutMilliseconds = 1000)
    {
        if (Comms.com is not { IsOpen: true })
            return Task.FromResult(false);

        lock (_currentPositionLock)
        {
            if (_currentPositionRequest is { IsCompleted: false })
                return _currentPositionRequest;

            var request = RequestCurrentPositionCoreAsync(timeoutMilliseconds);
            _currentPositionRequest = request;
            _ = request.ContinueWith(_ =>
            {
                lock (_currentPositionLock)
                {
                    if (ReferenceEquals(_currentPositionRequest, request))
                        _currentPositionRequest = null;
                }
            }, TaskScheduler.Default);
            return request;
        }
    }

    async Task<bool> RequestCurrentPositionCoreAsync(int timeoutMilliseconds)
    {
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _awaitCoord = true;
        void statusProcessed(string data) => DataReceived(data, timeout.Token, completion);

        _parameters.OnRealtimeStatusProcessed += statusProcessed;
        try
        {
            using var registration = timeout.Token.Register(() => completion.TrySetResult(false));
            RequestStatus();
            return await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _parameters.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(_parameters.OnRealtimeStatusProcessed, statusProcessed)!;
            _awaitCoord = false;
        }
    }

    void DataReceived(string data, CancellationToken cancellationToken, TaskCompletionSource<bool> completion)
    {
        if (_awaitCoord)
        {
            if (TryCaptureMachinePosition())
            {
                _awaitCoord = false;
                completion.TrySetResult(true);
            }
            else
                _ = RequestStatusAfterDelayAsync(cancellationToken);
        }
        else
            completion.TrySetResult(true);
    }

    static async Task RequestStatusAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
