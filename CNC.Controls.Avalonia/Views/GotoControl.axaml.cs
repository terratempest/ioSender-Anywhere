using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class GotoControl : UserControl
{
    string _coordinateSystemCode = "G54";

    public GotoControl()
    {
        InitializeComponent();
        CoordinateSystems = new ObservableCollection<CoordinateSystem>();
        GrblWorkParameters.CoordinateSystems.CollectionChanged += OnCoordinateSystemsChanged;
    }

    public ObservableCollection<CoordinateSystem> CoordinateSystems { get; }

    void OnCoordinateSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cbxCoordinateSystem.SelectedItem is CoordinateSystem cs)
            _coordinateSystemCode = cs.Code;
    }

    void OnGotoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel grbl || sender is not Button btn)
            return;

        if ((string?)btn.Tag == "G5x")
        {
            var cs = GrblWorkParameters.GetCoordinateSystem(_coordinateSystemCode);
            if (cs != null)
                grbl.ExecuteCommand("G53G0" + cs.ToString(GrblInfo.AxisFlags));
        }
        else
            grbl.ExecuteCommand((string)btn.Tag!);
    }

    void OnCoordinateSystemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count == 1 &&
            e.NewItems[0] is CoordinateSystem cs &&
            cs.Code.StartsWith("G5", StringComparison.Ordinal))
        {
            CoordinateSystems.Add(cs);
        }
    }
}
