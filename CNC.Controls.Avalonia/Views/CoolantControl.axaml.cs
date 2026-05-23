using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class CoolantControl : UserControl
{
    public CoolantControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLocalization();
    }

    void ApplyLocalization()
    {
        Localize.Apply(LblCoolant);
        tswFlood.Label = LocalizedStrings.Get("CNC.Controls.WPF.coolantcontrol.lbl_flood", "Flood");
        tswMist.Label = LocalizedStrings.Get("CNC.Controls.WPF.coolantcontrol.lbl_mist", "Mist");
        tswFan.Label = LocalizedStrings.Get("CNC.Controls.WPF.coolantcontrol.lbl_fan", "Fan");
    }

    private void chkCoolant_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model || sender is not ToggleControl toggle)
            return;

        var tag = toggle.Tag as string;
        if (tag == "Flood")
            model.ExecuteCommand(GrblCommand.Flood);
        else if (tag == "Mist")
            model.ExecuteCommand(GrblCommand.Mist);
        else
            model.ExecuteCommand(GrblCommand.Fan);
    }
}
