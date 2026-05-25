using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class CoolantControl : UserControl
{
    GrblViewModel? _model;
    readonly Thickness _mistMarginWithFan = new(2, 0, 2, 0);
    readonly Thickness _mistMarginWithoutFan = new(2, 0, 0, 0);

    public CoolantControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLocalization();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_model != null)
            _model.PropertyChanged -= OnModelPropertyChanged;

        base.OnDataContextChanged(e);

        _model = DataContext as GrblViewModel;
        if (_model != null)
            _model.PropertyChanged += OnModelPropertyChanged;

        UpdateFanLayout();
    }

    void ApplyLocalization()
    {
        Localize.Apply(BtnFlood);
        Localize.Apply(BtnMist);
        Localize.Apply(BtnFan);
    }

    void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.HasFans))
            UpdateFanLayout();
    }

    void UpdateFanLayout()
    {
        var hasFans = _model?.HasFans == true;
        LayoutRoot.ColumnDefinitions[2].Width = hasFans
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        BtnMist.Margin = hasFans ? _mistMarginWithFan : _mistMarginWithoutFan;
    }

    void Coolant_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model || sender is not Control { Tag: string tag })
            return;

        switch (tag)
        {
            case "Flood":
                model.ExecuteCommand(GrblCommand.Flood);
                break;
            case "Mist":
                model.ExecuteCommand(GrblCommand.Mist);
                break;
            case "Fan":
                model.ExecuteCommand(GrblCommand.Fan);
                break;
        }
    }
}
