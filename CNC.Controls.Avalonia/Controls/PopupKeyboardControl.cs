using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CNC.Controls.Avalonia.Controls;

public sealed class PopupKeyboardControl : UserControl
{
    bool _shift;

    public event EventHandler<PopupKeyboardAction>? ActionInvoked;

    public PopupKeyboardControl()
    {
        Layout = PopupKeyboardLayout.Regular;
        UseGlobalTarget = true;
        MinWidth = 260;
        MinHeight = 160;
        Build();
    }

    public bool UseGlobalTarget { get; set; }

    public static readonly StyledProperty<PopupKeyboardLayout> LayoutProperty =
        AvaloniaProperty.Register<PopupKeyboardControl, PopupKeyboardLayout>(
            nameof(Layout),
            PopupKeyboardLayout.Regular);

    public PopupKeyboardLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
        {
            _shift = false;
            Build();
        }
    }

    void Build()
    {
        Content = Layout == PopupKeyboardLayout.Numeric
            ? BuildNumericPad()
            : BuildRegularKeyboard();
    }

    Grid BuildRegularKeyboard()
    {
        var grid = CreateRows(5);
        AddRow(grid, 0, ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "Back"]);
        AddRow(grid, 1, ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"]);
        AddRow(grid, 2, ["a", "s", "d", "f", "g", "h", "j", "k", "l", "Enter"]);
        AddRow(grid, 3, ["Shift", "z", "x", "c", "v", "b", "n", "m", ".", ",", "?"]);
        AddRow(grid, 4, ["Space", "Esc"]);
        return grid;
    }

    Grid BuildNumericPad()
    {
        var grid = CreateRows(4);
        AddRow(grid, 0, ["7", "8", "9", "Back"]);
        AddRow(grid, 1, ["4", "5", "6", "Clear"]);
        AddRow(grid, 2, ["1", "2", "3", "-"]);
        AddRow(grid, 3, ["0", ".", "Enter", "Esc"]);
        return grid;
    }

    static Grid CreateRows(int count)
    {
        var grid = new Grid
        {
            Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", count))),
            RowSpacing = 4,
        };
        return grid;
    }

    void AddRow(Grid parent, int rowIndex, IReadOnlyList<string> keys)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", keys.Select(k => k == "Space" ? "4*" : "*"))),
            ColumnSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var button = CreateKeyButton(key);
            Grid.SetColumn(button, i);
            row.Children.Add(button);
        }

        Grid.SetRow(row, rowIndex);
        parent.Children.Add(row);
    }

    Button CreateKeyButton(string key)
    {
        var button = new Button
        {
            Content = DisplayText(key),
            Tag = key,
            Focusable = false,
            FontSize = 16,
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinWidth = 28,
            MinHeight = 30,
        };
        button.Click += OnKeyClick;
        return button;
    }

    string DisplayText(string key) =>
        IsLetter(key) && _shift ? key.ToUpperInvariant() : key;

    void OnKeyClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key })
            return;

        PopupKeyboardAction? action = key switch
        {
            "Back" => PopupKeyboardAction.Backspace,
            "Clear" => PopupKeyboardAction.Clear,
            "Enter" => PopupKeyboardAction.Enter,
            "Esc" => PopupKeyboardAction.Escape,
            "Space" => PopupKeyboardAction.Insert(" "),
            _ => null,
        };

        if (key == "Shift")
        {
            _shift = !_shift;
            Build();
            return;
        }

        action ??= PopupKeyboardAction.Insert(IsLetter(key) && _shift ? key.ToUpperInvariant() : key);
        InvokeAction(action);
        if (_shift && IsLetter(key))
        {
            _shift = false;
            Build();
        }
    }

    void InvokeAction(PopupKeyboardAction action)
    {
        ActionInvoked?.Invoke(this, action);
        if (UseGlobalTarget && PopupKeyboardTarget.Current is { } target)
            TextBoxKeyboardEditor.Apply(target, action);
    }

    static bool IsLetter(string key) => key.Length == 1 && char.IsLetter(key[0]);
}
