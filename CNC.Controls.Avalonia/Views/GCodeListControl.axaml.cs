using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class GCodeListControl : UserControl
{
    readonly Dictionary<GCodeBlock, PropertyChangedEventHandler> _blockHandlers = new();

    GrblViewModel? _model;

    public static readonly StyledProperty<bool> SingleSelectedProperty =
        AvaloniaProperty.Register<GCodeListControl, bool>(nameof(SingleSelected));

    public static readonly StyledProperty<bool> MultipleSelectedProperty =
        AvaloniaProperty.Register<GCodeListControl, bool>(nameof(MultipleSelected));

    public GCodeListControl()
    {
        InitializeComponent();
        GCodeGrid.ContextMenu!.DataContext = this;
        DragDrop.SetAllowDrop(GCodeGrid, true);
        GCodeGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        GCodeGrid.AddHandler(DragDrop.DropEvent, OnDrop);
        GCodeFileService.Instance.ProgramLoading += OnProgramLoading;
        GCodeFileService.Instance.ProgramChanged += OnProgramChanged;
        Loaded += (_, _) =>
        {
            ApplyLocalization();
            BindProgram();
        };
        Unloaded += (_, _) => DetachModel();
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
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        BindProgram();
    }

    void DetachModel()
    {
        if (_model is INotifyPropertyChanged pc)
            pc.PropertyChanged -= OnViewModelPropertyChanged;
        _model = null;
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
        DetachBlockHandlers();
        GCodeGrid.ItemsSource = GCodeFileService.Instance.Data;
        if (_model != null && _model.ScrollPosition >= 0)
            ScrollToBlock(_model.ScrollPosition);
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel vm || e.PropertyName != nameof(GrblViewModel.ScrollPosition))
            return;

        ScrollToBlock(vm.ScrollPosition);
    }

    void ScrollToBlock(int index)
    {
        if (index < 0 || index >= GCodeFileService.Instance.Data.Count)
            return;

        var block = GCodeFileService.Instance.Data[index];
        GCodeGrid.SelectedItem = block;
        GCodeGrid.ScrollIntoView(block, null);
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
        row.Classes.Remove("gcode-done");

        var sent = block.Sent?.Replace("BRK ", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        if (sent == "*")
            row.Classes.Add("gcode-current");
        else if (sent is "ok" or "@" || sent.StartsWith("ok", StringComparison.Ordinal))
            row.Classes.Add("gcode-done");
    }

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model is null)
            return;

        var index = GCodeGrid.SelectedIndex;
        SingleSelected = GCodeGrid.SelectedItems.Count == 1 && _model.StartFromBlock.CanExecute(index);
        MultipleSelected = GCodeGrid.SelectedItems.Count >= 1 && _model.StartFromBlock.CanExecute(index);
    }

    void OnStartHereClick(object? sender, RoutedEventArgs e)
    {
        if (_model != null && GCodeGrid.SelectedIndex >= 0)
            _model.StartFromBlock.Execute(GCodeGrid.SelectedIndex);
    }

    void OnCopyMdiClick(object? sender, RoutedEventArgs e)
    {
        if (_model != null && GCodeGrid.SelectedItem is GCodeBlock block)
            _model.MDIText = block.DisplayData;
    }

    void OnToggleBreakClick(object? sender, RoutedEventArgs e)
    {
        if (GCodeGrid.SelectedItem is GCodeBlock block)
            block.BreakAt ^= true;
    }

    void OnSendControllerClick(object? sender, RoutedEventArgs e)
    {
        if (_model is null)
            return;

        var rows = GCodeGrid.SelectedItems.Cast<GCodeBlock>().OrderBy(b => b.Row).ToList();
        foreach (var row in rows)
            _model.ExecuteCommand(row.Data);
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return;

        var path = e.Data.GetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            GCodeFileService.Instance.Load(path);
    }
}
