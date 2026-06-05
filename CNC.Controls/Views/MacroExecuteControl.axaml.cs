using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Views;

public partial class MacroExecuteControl : UserControl
{
    public static readonly StyledProperty<ObservableCollection<Macro>?> MacrosProperty =
        AvaloniaProperty.Register<MacroExecuteControl, ObservableCollection<Macro>?>(nameof(Macros));

    public static readonly StyledProperty<bool> IsMessageVisibleProperty =
        AvaloniaProperty.Register<MacroExecuteControl, bool>(nameof(IsMessageVisible), true);

    readonly AppConfigService? _appConfig;
    ObservableCollection<Macro>? _subscribedMacros;
    bool _editorOpen;

    public MacroExecuteControl()
    {
        InitializeComponent();
    }

    public MacroExecuteControl(AppConfigService appConfig) : this()
    {
        _appConfig = appConfig;
        Macros = appConfig.Base.Macros;
    }

    public ObservableCollection<Macro>? Macros
    {
        get => GetValue(MacrosProperty);
        set => SetValue(MacrosProperty, value);
    }

    public bool IsMessageVisible
    {
        get => GetValue(IsMessageVisibleProperty);
        set => SetValue(IsMessageVisibleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MacrosProperty)
            SubscribeMacros(change.OldValue as ObservableCollection<Macro>, change.NewValue as ObservableCollection<Macro>);
    }

    void SubscribeMacros(ObservableCollection<Macro>? oldValue, ObservableCollection<Macro>? newValue)
    {
        if (oldValue != null)
            oldValue.CollectionChanged -= OnMacrosCollectionChanged;
        if (_subscribedMacros != null && !ReferenceEquals(_subscribedMacros, oldValue))
            _subscribedMacros.CollectionChanged -= OnMacrosCollectionChanged;

        _subscribedMacros = newValue;
        if (newValue != null)
            newValue.CollectionChanged += OnMacrosCollectionChanged;

        UpdateMessageVisibility();
    }

    void OnMacrosCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateMessageVisibility();
    }

    void UpdateMessageVisibility() => IsMessageVisible = Macros == null || Macros.Count == 0;

    void OnMacroButtonLoaded(object? sender, RoutedEventArgs e) => SetMacroButtonLabel(sender as Button);

    static void SetMacroButtonLabel(Button? button)
    {
        if (button?.Tag is Macro macro)
            button.Content = GetMacroButtonLabel(macro);
    }

    static string GetMacroButtonLabel(Macro macro) =>
        macro.Id is >= 1 and <= 12
            ? $"{macro.Name} (F{macro.Id})"
            : macro.Name;

    void OnMacroClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Macro macro } || DataContext is not GrblViewModel model)
            return;

        if (model.IsJobRunning)
            return;

        if (macro.ConfirmOnExecute &&
            !MessageDialogs.AskYesNo($"Run {macro.Name} macro?", "Run macro"))
            return;

        model.ExecuteMacro(macro.Code);
    }

    async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (_appConfig == null || _editorOpen)
            return;

        _editorOpen = true;
        if (sender is Button button)
            button.IsEnabled = false;

        try
        {
            if (await MacroEditor.ShowAsync(_appConfig.Base.Macros, GetMainWindow()))
            {
                _appConfig.Save();
                Macros = _appConfig.Base.Macros;
            }
        }
        finally
        {
            _editorOpen = false;
            if (sender is Button editButton)
                editButton.IsEnabled = true;
        }
    }

    static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
