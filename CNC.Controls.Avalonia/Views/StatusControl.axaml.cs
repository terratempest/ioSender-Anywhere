using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class StatusControl : UserControl
{
    public StatusControl()
    {
        InitializeComponent();
        ApplyLocalization();
        btnUnlock.Click += Unlock_Click;
        btnReset.Click += Reset_Click;
    }

    void ApplyLocalization()
    {
        Localize.Apply(LblState);
        Localize.Apply(btnUnlock);
        Localize.Apply(btnReset);
    }

    void Unlock_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model)
            return;

        model.ExecuteCommand(GrblConstants.CMD_UNLOCK);
    }

    void Reset_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model)
            return;

        if (model.GrblState.State == GrblStates.Alarm &&
            model.GrblState.Substate == 10 &&
            model.Signals.Value.HasFlag(Signals.EStop))
        {
            GrblUi.ShowError("Clear E-Stop before <Reset>", "ioSender");
            return;
        }

        Grbl.Reset();
    }
}
