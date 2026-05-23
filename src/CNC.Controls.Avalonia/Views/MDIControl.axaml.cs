using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class MDIControl : UserControl
{
    public static readonly StyledProperty<string> CommandProperty =
        AvaloniaProperty.Register<MDIControl, string>(nameof(Command), string.Empty);

    public static readonly StyledProperty<ObservableCollection<string>> CommandsProperty =
        AvaloniaProperty.Register<MDIControl, ObservableCollection<string>>(nameof(Commands));

    public MDIControl()
    {
        InitializeComponent();
        Commands = new ObservableCollection<string>();
        Loaded += (_, _) =>
        {
            Localize.Apply(LblMdi);
            Localize.Apply(BtnSend);
        };
    }

    public string Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public ObservableCollection<string> Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => SendMdi();

    private void OnMdiSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MdiCombo.SelectedItem is string cmd)
            Command = cmd;
    }

    private void OnMdiKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendMdi();
            e.Handled = true;
        }
    }

    private void SendMdi()
    {
        if (DataContext is GrblViewModel vm && !string.IsNullOrWhiteSpace(Command))
        {
            vm.MDICommand.Execute(Command.Trim());
            if (!Commands.Contains(Command))
                Commands.Add(Command);
        }
    }
}
