using Avalonia.Controls;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class ErrorsAndAlarms : Window
{
    public ErrorsAndAlarms() : this("ioSender")
    {
    }

    public ErrorsAndAlarms(string title)
    {
        InitializeComponent();
        ApplyLocalization(title);
    }

    private void ApplyLocalization(string title)
    {
        Localize.Apply(ErrorsAndAlarmsRoot);
        Localize.Apply(TabErrors);
        Localize.Apply(TabAlarms);

        Title = title + " - " + Title;
    }
}
