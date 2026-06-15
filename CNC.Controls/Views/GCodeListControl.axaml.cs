using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class GCodeListControl : UserControl
{
    readonly GCodeListViewModel _viewModel;
    readonly Dictionary<GCodeBlock, PropertyChangedEventHandler> _blockHandlers = new();

    GrblViewModel? _model;

    public static readonly StyledProperty<bool> SingleSelectedProperty =
        AvaloniaProperty.Register<GCodeListControl, bool>(nameof(SingleSelected));

    public static readonly StyledProperty<bool> MultipleSelectedProperty =
        AvaloniaProperty.Register<GCodeListControl, bool>(nameof(MultipleSelected));

    public GCodeListControl(ProgramService? program = null, MachineCommandService? commands = null)
    {
        _viewModel = new(program, commands);
        InitializeComponent();
        GCodeGrid.ContextMenu!.DataContext = this;
        DragDrop.SetAllowDrop(GCodeGrid, true);
        GCodeGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        GCodeGrid.AddHandler(DragDrop.DropEvent, OnDrop);
        _viewModel.ProgramLoading += OnProgramLoading;
        _viewModel.ProgramChanged += OnProgramChanged;
        Loaded += (_, _) =>
        {
            ApplyLocalization();
            BindProgram();
        };
        Unloaded += (_, _) =>
        {
            DetachModel();
            DetachBlockHandlers();
        };
    }

    public GCodeListControl() : this(null, null)
    {
    }

    void ApplyLocalization()
    {
        Localize.Apply(MnuSendToController);
        Localize.Apply(MnuStartFromHere);
        Localize.Apply(MnuCopyToMdi);
        Localize.Apply(MnuToggleBreak);
    }

    public bool SingleSelected
    {
        get => GetValue(SingleSelectedProperty);
        private set => SetValue(SingleSelectedProperty, value);
    }

    public bool MultipleSelected
    {
        get => GetValue(MultipleSelectedProperty);
        private set => SetValue(MultipleSelectedProperty, value);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        DetachModel();
        if (DataContext is GrblViewModel vm)
        {
            _model = vm;
            _viewModel.Model = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        else
        {
            _viewModel.Model = null;
        }
        BindProgram();
    }

    void DetachModel()
    {
        if (_model is INotifyPropertyChanged pc)
            pc.PropertyChanged -= OnViewModelPropertyChanged;
        _model = null;
        _viewModel.Model = null;
    }

    void OnProgramLoading()
    {
        GCodeGrid.ItemsSource = null;
        DetachBlockHandlers();
    }

    void OnProgramChanged() => BindProgram();

    void DetachBlockHandlers()
    {
        foreach (var (block, handler) in _blockHandlers.ToList())
            block.PropertyChanged -= handler;
        _blockHandlers.Clear();
    }

    void BindProgram()
    {
#if DEBUG
        var watch = Stopwatch.StartNew();
#endif
        DetachBlockHandlers();
        GCodeGrid.ItemsSource = _viewModel.Data;
        if (_model != null && _model.ScrollPosition >= 0)
            ScrollToBlock(_model.ScrollPosition);
#if DEBUG
        watch.Stop();
        Trace.WriteLine($"G-code grid bind completed in {watch.ElapsedMilliseconds} ms; rows={_viewModel.Data.Count}");
#endif
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel vm || e.PropertyName != nameof(GrblViewModel.ScrollPosition))
            return;

        ScrollToBlock(vm.ScrollPosition);
    }

    void ScrollToBlock(int index)
    {
        if (index < 0 || index >= _viewModel.Data.Count)
            return;

        var block = _viewModel.Data[index];
        GCodeGrid.ScrollIntoView(block, null);
        Dispatcher.UIThread.Post(() => CenterBlockInView(index, block), DispatcherPriority.Background);
    }

    void CenterBlockInView(int index, GCodeBlock block)
    {
        var scrollViewer = GCodeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer == null)
            return;

        if (UsesLogicalScroll(scrollViewer, _viewModel.Data.Count))
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, CalculateCenteredLogicalOffset(
                index,
                scrollViewer.Viewport.Height,
                scrollViewer.ScrollBarMaximum.Y));
            return;
        }

        var row = GCodeGrid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(row => ReferenceEquals(row.DataContext, block));

        if (row == null)
            return;

        var center = row.TranslatePoint(new Point(row.Bounds.Width / 2d, row.Bounds.Height / 2d), scrollViewer);
        if (center == null)
            return;

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, CalculateCenteredPixelOffset(
            scrollViewer.Offset.Y,
            center.Value.Y,
            scrollViewer.Bounds.Height,
            scrollViewer.ScrollBarMaximum.Y));
    }

    static bool UsesLogicalScroll(ScrollViewer scrollViewer, int itemCount) =>
        scrollViewer.ScrollBarMaximum.Y <= itemCount;

    internal static double CalculateCenteredLogicalOffset(
        int index,
        double viewportHeight,
        double maxOffsetY)
    {
        var max = Math.Max(0d, maxOffsetY);
        var centered = index - (viewportHeight / 2d);
        return Math.Clamp(centered, 0d, max);
    }

    internal static double CalculateCenteredPixelOffset(
        double currentOffsetY,
        double rowCenterY,
        double viewportHeight,
        double maxOffsetY)
    {
        var max = Math.Max(0d, maxOffsetY);
        var centered = currentOffsetY + rowCenterY - (viewportHeight / 2d);
        return Math.Clamp(centered, 0d, max);
    }

    void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not GCodeBlock block)
            return;

        ApplyRowState(e.Row, block);
        if (_blockHandlers.ContainsKey(block))
            return;

        void Handler(object? _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(GCodeBlock.Sent) or nameof(GCodeBlock.BreakAt))
                ApplyRowState(e.Row, block);
        }

        _blockHandlers[block] = Handler;
        block.PropertyChanged += Handler;
    }

    void OnUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not GCodeBlock block)
            return;

        if (_blockHandlers.Remove(block, out var handler))
            block.PropertyChanged -= handler;
    }

    static void ApplyRowState(DataGridRow row, GCodeBlock block)
    {
        row.Classes.Remove("gcode-current");
        row.Classes.Remove("gcode-pending");
        row.Classes.Remove("gcode-done");

        var statusClass = GetRowStatusClass(block.Sent);
        if (statusClass != null)
            row.Classes.Add(statusClass);
    }

    internal static string? GetRowStatusClass(string? sent)
    {
        sent = sent?.Replace("BRK ", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return sent switch
        {
            "@" => "gcode-current",
            "pending" => "gcode-pending",
            "ok" => "gcode-done",
            _ when sent.StartsWith("ok", StringComparison.Ordinal) => "gcode-done",
            _ => null
        };
    }

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model is null)
            return;

        var index = GCodeGrid.SelectedIndex;
        SingleSelected = GCodeGrid.SelectedItems.Count == 1 && _viewModel.CanStartFrom(index);
        MultipleSelected = GCodeGrid.SelectedItems.Count >= 1 && _viewModel.CanStartFrom(index);
    }

    void OnStartHereClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.StartFrom(GCodeGrid.SelectedIndex);
    }

    void OnCopyMdiClick(object? sender, RoutedEventArgs e)
    {
        if (GCodeGrid.SelectedItem is GCodeBlock block)
            _viewModel.CopyToMdi(block);
    }

    void OnToggleBreakClick(object? sender, RoutedEventArgs e)
    {
        if (GCodeGrid.SelectedItem is GCodeBlock block)
            _viewModel.ToggleBreak(block);
    }

    void OnSendControllerClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SendToController(GCodeGrid.SelectedItems.Cast<GCodeBlock>());
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;

        var path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
            _viewModel.LoadDroppedFile(path);
    }
}
