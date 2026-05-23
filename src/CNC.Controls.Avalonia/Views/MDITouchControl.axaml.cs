using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class MDITouchControl : UserControl
{
    public static readonly StyledProperty<string> CommandProperty =
        AvaloniaProperty.Register<MDITouchControl, string>(nameof(Command), string.Empty);

    public static readonly StyledProperty<ObservableCollection<string>> CommandsProperty =
        AvaloniaProperty.Register<MDITouchControl, ObservableCollection<string>>(nameof(Commands));

    public MDITouchControl()
    {
        InitializeComponent();
        Commands = new ObservableCollection<string>();
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

    void OnEnterClick(object? sender, RoutedEventArgs e) => SendMdi();

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

    void OnKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: { } content })
            InsertText(content.ToString() ?? string.Empty);
    }

    void OnSpaceClick(object? sender, RoutedEventArgs e) => InsertText(" ");

    void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        var text = Command ?? string.Empty;
        var start = Math.Clamp(MdiText.SelectionStart, 0, text.Length);
        var end = Math.Clamp(MdiText.SelectionEnd, 0, text.Length);

        if (start != end)
        {
            var left = Math.Min(start, end);
            var right = Math.Max(start, end);
            Command = text.Remove(left, right - left);
            MdiText.CaretIndex = left;
        }
        else if (start > 0)
        {
            Command = text.Remove(start - 1, 1);
            MdiText.CaretIndex = start - 1;
        }

        MdiText.Focus();
    }

    void InsertText(string value)
    {
        var text = Command ?? string.Empty;
        var start = Math.Clamp(MdiText.SelectionStart, 0, text.Length);
        var end = Math.Clamp(MdiText.SelectionEnd, 0, text.Length);
        var left = Math.Min(start, end);
        var right = Math.Max(start, end);

        Command = text.Remove(left, right - left).Insert(left, value);
        MdiText.CaretIndex = left + value.Length;
        MdiText.Focus();
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
