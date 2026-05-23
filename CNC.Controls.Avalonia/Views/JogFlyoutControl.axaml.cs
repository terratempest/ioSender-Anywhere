using System.ComponentModel;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class JogFlyoutControl : FlyoutControlBase
{
    public JogFlyoutControl() : base("_Jog") => InitializeComponent();

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel grbl)
            grbl.PropertyChanged += OnGrblPropertyChanged;
    }

    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GrblViewModel grbl &&
            e.PropertyName == nameof(GrblViewModel.StreamingState) &&
            IsVisible &&
            grbl.IsJobRunning)
        {
            HideFlyout();
        }
    }
}
