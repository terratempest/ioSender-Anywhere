using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using CNC.Core;

namespace CNC.Controls.Probing;

public partial class ProbeVerifyWindow : Window
{
    ProbingViewModel? _model;

    public ProbeVerifyWindow()
    {
        InitializeComponent();
    }

    public ProbeVerifyWindow(ProbingViewModel model)
        : this()
    {
        _model = model;
        model.Grbl!.PropertyChanged += Grbl_PropertyChanged;
        Closed += (_, _) => model.Grbl!.PropertyChanged -= Grbl_PropertyChanged;
    }

    public void ShowBlocking()
    {
        var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (owner != null)
            ShowDialog(owner);
        else
            Show();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == global::Avalonia.Input.Key.Escape)
            Close();
    }

    void Grbl_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.Signals) &&
            sender is GrblViewModel grbl &&
            grbl.Signals.Value.HasFlag(Signals.Probe))
        {
            if (_model == null)
                return;
            _model.ProbeVerified = true;
            Close(true);
        }
    }
}
