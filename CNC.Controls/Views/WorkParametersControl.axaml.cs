using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class WorkParametersControl : UserControl
{
    GrblViewModel? _model;
    bool _isAttached;

    public WorkParametersControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Localize.Apply(LblOffset);
            Localize.Apply(LblTool);
        };
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            GrblWorkParameters.CoordinateSystems.CollectionChanged -= CoordinateSystems_CollectionChanged;
            GrblWorkParameters.CoordinateSystems.CollectionChanged += CoordinateSystems_CollectionChanged;
            AttachModel(DataContext as GrblViewModel);
            SyncOffsetSelection();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAttached = false;
            GrblWorkParameters.CoordinateSystems.CollectionChanged -= CoordinateSystems_CollectionChanged;
            AttachModel(null);
        };
        DataContextChanged += (_, _) =>
        {
            if (_isAttached)
                AttachModel(DataContext as GrblViewModel);
            SyncOffsetSelection();
        };
    }

    public bool IsFocusedOnCombo => cbxTool.IsFocused || cbxOffset.IsFocused;

    void AttachModel(GrblViewModel? model)
    {
        if (ReferenceEquals(_model, model))
            return;

        if (_model != null)
            _model.PropertyChanged -= Model_PropertyChanged;

        _model = model;

        if (_model != null)
            _model.PropertyChanged += Model_PropertyChanged;
    }

    void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.WorkCoordinateSystem))
            SyncOffsetSelection();
    }

    void CoordinateSystems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncOffsetSelection();

    void SyncOffsetSelection()
    {
        if (DataContext is not GrblViewModel model || string.IsNullOrEmpty(model.WorkCoordinateSystem))
        {
            cbxOffset.SelectedItem = null;
            return;
        }

        var selected = GrblWorkParameters.CoordinateSystems
            .FirstOrDefault(cs => cs.Code == model.WorkCoordinateSystem);

        if (!ReferenceEquals(cbxOffset.SelectedItem, selected))
            cbxOffset.SelectedItem = selected;
    }

    private void cbxOffset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && sender is ComboBox { IsDropDownOpen: true } &&
            DataContext is GrblViewModel model && e.AddedItems[0] is CoordinateSystem cs)
            model.ExecuteCommand(cs.Code);
    }

    private void cbxTool_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && sender is ComboBox { IsDropDownOpen: true } &&
            DataContext is GrblViewModel model && e.AddedItems[0] is Tool tool)
            model.ExecuteCommand(string.Format(GrblCommand.ToolChange, tool.Id == -1 ? 0 : tool.Id).ToString());
    }
}
