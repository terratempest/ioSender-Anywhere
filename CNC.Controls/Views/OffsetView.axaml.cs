using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.GCode;
using Avalonia.Data;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CNC.Controls.Avalonia.Converters;

namespace CNC.Controls.Avalonia.Views;

public partial class OffsetView : UserControl{
    readonly GrblViewModel _parameters = new();
    readonly object _currentPositionLock = new();

    CoordinateSystem? _selectedOffset;
    volatile bool _awaitCoord;
    Task<bool>? _currentPositionRequest;

    public static readonly StyledProperty<bool> CanEditProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(CanEdit));

    public static readonly StyledProperty<bool> IsPredefinedProperty =
        AvaloniaProperty.Register<OffsetView, bool>(nameof(IsPredefined));

    public OffsetView(){
        InitializeComponent();

        if (Design.IsDesignMode){
            OffsetAxes.Add(new OffsetAxisRow(this, "X", 0, AxisFlags.X));
            OffsetAxes.Add(new OffsetAxisRow(this, "Y", 1, AxisFlags.Y));
            OffsetAxes.Add(new OffsetAxisRow(this, "Z", 2, AxisFlags.Z));

            OffsetAxes[0].Value = 13323.456;
            OffsetAxes[1].Value = 78.900;
            OffsetAxes[2].Value = -5.250;

            Offset.Code = "G54";

            BuildOffsetGridColumns();
            return;
        }

        _parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
        if (!GrblInfo.IsGrblHAL)
            _parameters.PropertyChanged += Parameters_PropertyChanged;
    }

    public CoordinateSystem Offset{ get; } = new();

    public ObservableCollection<OffsetAxisRow> OffsetAxes{ get; } = new();

    public bool CanEdit{
        get => GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public bool IsPredefined{
        get => GetValue(IsPredefinedProperty);
        set => SetValue(IsPredefinedProperty, value);
    }

    public sealed class OffsetAxisRow : INotifyPropertyChanged{
        readonly OffsetView _owner;

        string _header;
        string _label;

        public OffsetAxisRow(
        OffsetView owner,
        string axis,
        int index,
        AxisFlags flag){
            _owner = owner;

            Axis = axis;
            Index = index;
            Flag = flag;

            _header = axis;
            _label = $"{axis} axis:";
        }

        public string Axis{ get; }

        public int Index{ get; }

        public AxisFlags Flag{ get; }

        public string Header{
            get => _header;
            set{
                if (_header == value)
                    return;

                _header = value;
                OnPropertyChanged(nameof(Header));
            }
        }

        public string Label{
            get => _label;
            set{
                if (_label == value)
                    return;

                _label = value;
                OnPropertyChanged(nameof(Label));
            }
        }

        public bool IsVisible =>
            GrblInfo.AxisFlags.HasFlag(Flag);

        public double Value{
            get => _owner.Offset.Values[Index];
            set{
                if (_owner.Offset.Values[Index] == value)
                    return;

                _owner.Offset.Values[Index] = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public void Refresh(){
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(Header));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void OnPropertyChanged(string propertyName){
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }

    void BuildAxes(){
        OffsetAxes.Clear();

        for (var i = 0; i < GrblInfo.AxisLetters.Length && i < Offset.Values.Length; i++){
            var axis = GrblInfo.AxisLetters[i].ToString();
            var flag = (AxisFlags)(1 << i);

            if (!GrblInfo.AxisFlags.HasFlag(flag))
                continue;

            OffsetAxes.Add(new OffsetAxisRow(this, axis, i, flag));
        }
    }

    public void Activate(bool activate){
        var comms = Comms.com;

        if (activate){
            if (comms is{ IsOpen: true }){
                comms.DataReceived += _parameters.DataReceived;
                GrblWorkParameters.Get(_parameters);
            }

            if (DataContext is GrblViewModel vm)
                vm.AxisEnabledFlags = GrblInfo.AxisFlags;

            BuildAxes();
            ApplyLatheColumnHeaders();
            BuildOffsetGridColumns();

            dgrOffsets.ItemsSource = GrblWorkParameters.CoordinateSystems;
            dgrOffsets.SelectedIndex = 0;
        }
        else{
            if (comms != null){
                comms.DataReceived -= _parameters.DataReceived;
                comms.PurgeQueue();
            }

            dgrOffsets.ItemsSource = null;
        }
    }

    void ApplyLatheColumnHeaders(){
        if (!GrblInfo.LatheModeEnabled)
            return;

        var xAxis = OffsetAxes.FirstOrDefault(a => a.Axis == "X");
        if (xAxis == null)
            return;

        var header = $"X offset ({(GrblWorkParameters.LatheMode == LatheMode.Radius ? "R" : "D")})";

        xAxis.Header = header;
        xAxis.Label = header + ":";
    }
    void BuildOffsetGridColumns(){
        if (dgrOffsets == null)
            return;

        dgrOffsets.Columns.Clear();

        dgrOffsets.Columns.Add(new DataGridTemplateColumn{
            Header = "Offset",
            CellTemplate = BuildOffsetCellTemplate(),
            Width = new DataGridLength(80, DataGridLengthUnitType.Pixel)
        });

        foreach (var axis in OffsetAxes){
            dgrOffsets.Columns.Add(new DataGridTemplateColumn{
                HeaderTemplate = BuildAxisHeaderTemplate(axis),
                CellTemplate = BuildAxisCellTemplate(axis),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 60,
                MaxWidth = 120
            });
        }
    }
    void RefreshOffsetsGrid(){
        dgrOffsets.ItemsSource = null;
        dgrOffsets.ItemsSource = GrblWorkParameters.CoordinateSystems;

        if (_selectedOffset != null)
            dgrOffsets.SelectedItem = _selectedOffset;
    }    
    IDataTemplate BuildOffsetCellTemplate(){
        return new FuncDataTemplate<CoordinateSystem>((row, _) =>{
            var accent = new Border{
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            accent.Classes.Add("offset-accent");
            accent[!Border.BackgroundProperty] = new MultiBinding{
                Bindings ={
                    new Binding("IsSelected"){
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor){
                            AncestorType = typeof(DataGridRow)
                        }
                    },
                    new DynamicResourceExtension("ThemeAccentBrush"),
                    new Binding{ Source = Brushes.Transparent }
                },
                Converter = new BoolToBrushConverter()
            };

            var text = new TextBlock{
                Text = row?.Code ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left,
                Margin = new Thickness(8, 0, 4, 0)
            };
            text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("ThemeForegroundBrush");
            Grid.SetColumn(text, 1);

            return new Grid{
                ColumnDefinitions = new ColumnDefinitions("4,*"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Children ={
                    accent,
                    text
                }
            };
        });
    }

    IDataTemplate BuildAxisHeaderTemplate(OffsetAxisRow axis){
        return new FuncDataTemplate<object>((_, _) =>{
            var brush = AxisBrush(axis.Axis);

            return new Border{
                Background = Brushes.Transparent,
                BorderBrush = brush,
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(0, 0, 0, 2),
                
                Child = new TextBlock{
                    Text = axis.Header,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = brush
                }
            };
        });
    }

    IDataTemplate BuildAxisCellTemplate(OffsetAxisRow axis){
        return new FuncDataTemplate<CoordinateSystem>((row, _) =>{
            var text = new TextBlock{
                Text = row == null ? string.Empty : row.Values[axis.Index].ToString("0.000"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 0)
            };

            var brush = AxisBrush(axis.Axis);
            if (brush != null)
                text.Foreground = brush;

            return text;
        });
    }

    IBrush? AxisBrush(string axis) => axis switch{
        "X" => Brushes.IndianRed,
        "Y" => Brushes.ForestGreen,
        "Z" => Brushes.DodgerBlue,
        _ => null
    };

    void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e){
        if (e.PropertyName == nameof(GrblViewModel.GrblError))
            GrblWorkParameters.Get(_parameters);
    }

    void dgrOffsets_SelectionChanged(object? sender, SelectionChangedEventArgs e){
        if (e.AddedItems.Count != 1 || e.AddedItems[0] is not CoordinateSystem row){
            _selectedOffset = null;
            return;
        }

        _selectedOffset = row;
        IsPredefined = row.Code is "G28" or "G30";

        for (var i = 0; i < Offset.Values.Length; i++)
            Offset.Values[i] = row.Values[i];
        
        foreach (var axis in OffsetAxes)
            axis.Refresh();

        Offset.Code = row.Code;

        if (IsPredefined)
            btnCurrPos_Click(null, new RoutedEventArgs());

        CanEdit = !IsPredefined;
    }

    void saveOffset(string axis){
        if (_selectedOffset == null || DataContext is not GrblViewModel model)
            return;

        var newpos = new Position(Offset);

        newpos.X = GrblWorkParameters.ConvertX(
            GrblWorkParameters.LatheMode,
            GrblParserState.LatheMode,
            _selectedOffset.X);

        GrblParserState.Get();

        var mChanged = GrblParserState.IsMetric != model.IsMetric;
        var cmd = mChanged ? model.IsMetric ? "G21" : "G20" : string.Empty;

        model.Message = string.Empty;

        if (_selectedOffset.Id == 0){
            var code = _selectedOffset.Code is "G28" or "G30"
                ? _selectedOffset.Code + ".1"
                : _selectedOffset.Code;

            cmd += axis == "ClearAll" || IsPredefined
                ? _selectedOffset.Code == "G43.1" ? "G49" : _selectedOffset.Code + ".1"
                : $"G90{code}{newpos.ToString(axis == "All" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis))}";
        }
        else{
            cmd += $"G90G10L2P{_selectedOffset.Id}{newpos.ToString(axis is "All" or "ClearAll" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis))}";
        }

        Comms.com?.WriteCommand(cmd);

        if (mChanged)
            Comms.com?.WriteCommand(model.IsMetric ? "G20" : "G21");
    }

    void cvOffset_Click(object? sender, RoutedEventArgs e){
        if (_selectedOffset == null || sender is not CoordValueSetControl2 control)
            return;

        var axis = control.Tag as string ?? "X";
        var index = GrblInfo.AxisLetterToIndex(axis);

        _selectedOffset.Values[index] = Offset.Values[index];

        RefreshOffsetsGrid();

        saveOffset(axis);
    }

    void btnSetAll_Click(object? sender, RoutedEventArgs e){
        if (_selectedOffset == null)
            return;

        for (var i = 0; i < Offset.Values.Length; i++)
            _selectedOffset.Values[i] = Offset.Values[i];

        RefreshOffsetsGrid();

        saveOffset("All");
    }

    void btnClearAll_Click(object? sender, RoutedEventArgs e){
        if (_selectedOffset == null)
            return;

        for (var i = 0; i < Offset.Values.Length; i++)
            Offset.Values[i] = _selectedOffset.Values[i] = 0d;

        foreach (var axis in OffsetAxes)
            axis.Refresh();

        RefreshOffsetsGrid();

        saveOffset("ClearAll");
    }

    void btnCurrPos_Click(object? sender, RoutedEventArgs e){
        _ = RequestCurrentPositionAsync();
    }

    void RequestStatus(){
        _parameters.WorkPositionOffset.Z = double.NaN;
        Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));
    }

    bool TryCaptureMachinePosition(){
        if (double.IsNaN(_parameters.MachinePosition.Values[0]))
            return false;

        Offset.Set(_parameters.MachinePosition);

        foreach (var axis in OffsetAxes)
            axis.Refresh();

        _parameters.SuspendPositionNotifications = true;
        _parameters.Clear();
        _parameters.MachinePosition.Clear();
        _parameters.SuspendPositionNotifications = false;

        return true;
    }

    internal Task<bool> RequestCurrentPositionAsync(int timeoutMilliseconds = 1000){
        if (Comms.com is not{ IsOpen: true })
            return Task.FromResult(false);

        lock (_currentPositionLock){
            if (_currentPositionRequest is{ IsCompleted: false })
                return _currentPositionRequest;

            var request = RequestCurrentPositionCoreAsync(timeoutMilliseconds);
            _currentPositionRequest = request;

            _ = request.ContinueWith(_ =>{
                lock (_currentPositionLock){
                    if (ReferenceEquals(_currentPositionRequest, request))
                        _currentPositionRequest = null;
                }
            }, TaskScheduler.Default);

            return request;
        }
    }

    async Task<bool> RequestCurrentPositionCoreAsync(int timeoutMilliseconds){
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _awaitCoord = true;

        void statusProcessed(string data) =>
            DataReceived(data, timeout.Token, completion);

        _parameters.OnRealtimeStatusProcessed += statusProcessed;

        try{
            using var registration = timeout.Token.Register(() => completion.TrySetResult(false));
            RequestStatus();
            return await completion.Task.ConfigureAwait(false);
        }
        finally{
            _parameters.OnRealtimeStatusProcessed =
                (Action<string>)Delegate.Remove(_parameters.OnRealtimeStatusProcessed, statusProcessed)!;

            _awaitCoord = false;
        }
    }

    void DataReceived(string data, CancellationToken cancellationToken, TaskCompletionSource<bool> completion){
        if (!_awaitCoord){
            completion.TrySetResult(true);
            return;
        }

        if (TryCaptureMachinePosition()){
            _awaitCoord = false;
            completion.TrySetResult(true);
        }
        else{
            _ = RequestStatusAfterDelayAsync(cancellationToken);
        }
    }

    static async Task RequestStatusAfterDelayAsync(CancellationToken cancellationToken){
        try{
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));
        }
        catch (OperationCanceledException){
        }
    }
}
