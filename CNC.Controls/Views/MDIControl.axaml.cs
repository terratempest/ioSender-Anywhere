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
        if (Design.IsDesignMode)
        {
            Command = "G0 X0 Y0";
            Commands.Add("$H");
            Commands.Add("G0 X0 Y0");
        }

        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
                return;

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

    void OnSendClick(object? sender, RoutedEventArgs e) => SendMdi();

    void OnMdiKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendMdi();
            e.Handled = true;
        }
    }

    void OnHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (Commands.Count == 0)
            return;

        var menu = new ContextMenu();
        foreach (var command in Commands.Reverse())
        {
            var item = new MenuItem { Header = command };
            item.Click += (_, _) =>
            {
                Command = command;
                MdiText.CaretIndex = Command.Length;
                MdiText.Focus();
            };
            menu.Items.Add(item);
        }

        menu.Open(BtnHistory);
    }

    void SendMdi()
    {
        var command = Command.Trim();
        if (DataContext is not GrblViewModel vm || string.IsNullOrWhiteSpace(command))
            return;

        vm.MDICommand.Execute(command);
        if (!Commands.Contains(command))
            Commands.Add(command);
        Command = string.Empty;
        MdiText.Focus();
    }
}
