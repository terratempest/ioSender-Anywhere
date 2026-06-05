using Avalonia.Controls;

using Avalonia.Interactivity;

using CNC.App;

using CNC.Core;



namespace CNC.Controls.Config;



public partial class AppKeyboardConfigPanel : UserControl

{

    BaseConfig? _config;



    public AppKeyboardConfigPanel()
    {
        InitializeComponent();
        PopupKeyboardTriggerCombo.ItemsSource = PopupKeyboardTriggerEntries;
        JogModeCombo.ItemsSource = Enum.GetValues<JogConfig.JogMode>();
        DataContextChanged += (_, _) => BindJogMode();
    }

    sealed class PopupKeyboardTriggerEntry
    {
        public PopupKeyboardTriggerEntry(PopupKeyboardTrigger value, string label)
        {
            Value = value;
            Label = label;
        }

        public PopupKeyboardTrigger Value { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    static readonly PopupKeyboardTriggerEntry[] PopupKeyboardTriggerEntries =
    [
        new(PopupKeyboardTrigger.Off, "Off"),
        new(PopupKeyboardTrigger.OneClick, "1 Click"),
        new(PopupKeyboardTrigger.TwoClick, "2 Click"),
    ];



    void BindJogMode()

    {

        _config = DataContext as BaseConfig;

        if (_config == null)

            return;



        PopupKeyboardTriggerCombo.SelectedItem = PopupKeyboardTriggerEntries
            .First(entry => entry.Value == _config.PopupKeyboardTrigger);

        JogModeCombo.SelectedItem = _config.Jog.Mode;

        RefreshKeyMapPath();

    }

    void OnPopupKeyboardTriggerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_config == null || PopupKeyboardTriggerCombo.SelectedItem is not PopupKeyboardTriggerEntry entry)
            return;

        _config.PopupKeyboardTrigger = entry.Value;
    }



    void OnJogModeChanged(object? sender, SelectionChangedEventArgs e)

    {

        if (_config == null || JogModeCombo.SelectedItem is not JogConfig.JogMode mode)

            return;



        _config.Jog.Mode = mode;

        RefreshKeyMapPath();

    }



    void OnSaveKeyMapClick(object? sender, RoutedEventArgs e) =>

        RunKeyMapAction(save: true);



    void OnLoadKeyMapClick(object? sender, RoutedEventArgs e) =>

        RunKeyMapAction(save: false);



    void RunKeyMapAction(bool save)

    {

        var keyboard = Grbl.GrblViewModel?.Keyboard;

        if (keyboard == null)

        {

            KeyMapStatusText.Text = "Machine view is not active; open the job view first.";

            return;

        }



        var path = KeyMapFilePath();

        var ok = save ? keyboard.SaveMappings(path) : keyboard.LoadMappings(path);

        KeyMapStatusText.Text = ok

            ? save ? $"Saved key mappings to {path}" : $"Loaded key mappings from {path}"

            : save ? "Save failed (no handlers or I/O error)." : "Load failed (missing file or I/O error).";

    }



    string KeyMapFilePath()

    {

        var mode = _config?.Jog.Mode ?? JogConfig.JogMode.UI;

        return Core.Resources.ConfigPath + $"KeyMap{(int)mode}.xml";

    }



    void RefreshKeyMapPath() => KeyMapPathText.Text = KeyMapFilePath();

}

