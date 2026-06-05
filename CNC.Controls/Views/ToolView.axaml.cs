using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Views;

public partial class ToolView : UserControl
{
    readonly GrblViewModel _parameters = new();
    Tool? _selectedTool;
    volatile bool _awaitCoord;
    Action<string>? _gotPosition;

    public ToolView()
    {
        InitializeComponent();
        Offset = new Position();
        _parameters.PropertyChanged += Parameters_PropertyChanged;
        AttachedToVisualTree += (_, _) => ApplyLatheColumnHeaders();
    }

    public Position Offset { get; }

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
            dgrTools.ItemsSource = new ObservableCollection<Tool>(
                GrblWorkParameters.Tools.Where(t => t.Code != GrblConstants.NO_TOOL).OrderBy(t => t.Id));
            dgrTools.SelectedIndex = 0;
        }
        else
        {
            if (comms != null)
                comms.DataReceived -= _parameters.DataReceived;
            dgrTools.ItemsSource = null;
        }
    }

    void ApplyLatheColumnHeaders()
    {
        if (!GrblInfo.LatheModeEnabled)
            return;
        cvXOffset.Label = $"X offset ({(GrblWorkParameters.LatheMode == LatheMode.Radius ? "R" : "D")}):";
    }

    void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.MachinePosition) &&
            !(_awaitCoord = double.IsNaN(_parameters.MachinePosition.Values[0])))
        {
            Offset.Set(_parameters.MachinePosition);
            _parameters.SuspendPositionNotifications = true;
            _parameters.Clear();
            _parameters.MachinePosition.Clear();
            _parameters.SuspendPositionNotifications = false;
        }
    }

    void dgrTools_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is Tool tool)
        {
            _selectedTool = tool;
            for (var i = 0; i < Offset.Values.Length; i++)
            {
                if (double.IsNaN(Offset.Values[i]))
                    Offset.Values[i] = 120;
                Offset.Values[i] = tool.Values[i];
            }
            txtTool.Text = tool.Code;
        }
        else
            _selectedTool = null;
    }

    void saveOffset(string axis)
    {
        if (_selectedTool == null)
            return;

        var newpos = new Position(Offset);
        newpos.X = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, _selectedTool.X);

        string axes = axis switch
        {
            "R" => "R{4}",
            "All" => newpos.ToString(GrblInfo.AxisFlags),
            _ => newpos.ToString(GrblInfo.AxisLetterToFlag(axis))
        };

        Comms.com?.WriteCommand(string.Format("G10L1P{0}{1}", _selectedTool.Code, axes));
    }

    void cvOffset_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedTool == null || sender is not CoordValueSetControl control)
            return;
        var axisletter = control.Tag as string ?? "X";
        var axis = GrblInfo.AxisLetterToIndex(axisletter);
        _selectedTool.Values[axis] = Offset.Values[axis];
        saveOffset(axisletter);
    }

    void btnSetAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;
        for (var i = 0; i < Offset.Values.Length; i++)
            _selectedTool.Values[i] = Offset.Values[i];
        saveOffset("All");
    }

    void btnClearAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;
        for (var i = 0; i < Offset.Values.Length; i++)
            Offset.Values[i] = _selectedTool.Values[i] = 0d;
        saveOffset("All");
    }

    void RequestStatus()
    {
        _parameters.Clear();
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
