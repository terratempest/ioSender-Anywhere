using System;
using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CNC.GCode;

namespace CNC.Controls.Lathe;

internal sealed class ProfileDialogViewModel : ProfileData
{
    LatheMode _xMode;

    public LatheMode XMode
    {
        get => _xMode;
        set { _xMode = value; OnPropertyChanged(); }
    }

    public bool XModeEnabled { get; set; }
    public bool ShowFeedFields { get; set; }
    public bool ShowRpmField { get; set; }
    public ObservableCollection<ProfileData> Profiles { get; } = new();

    ProfileData? _selected;

    public ProfileData? SelectedProfile
    {
        get => _selected;
        set
        {
            _selected = value;
            OnPropertyChanged();
        }
    }
}

public partial class ProfileDialog : Window
{
    WizardConfig _options = null!;
    readonly ProfileDialogViewModel _vm = new();

    public ProfileDialog()
    {
        InitializeComponent();
    }

    public ProfileDialog(WizardConfig options) : this()
    {
        _options = options;
        Title = $"{Title} - {options.ProfileName}";

        _vm.XMode = options.ActiveProfile.xmode;
        _vm.XModeEnabled = !options.ActiveProfile.xmodelock;
        _vm.ShowFeedFields = options.ProfileName != "Threading";
        _vm.ShowRpmField = options.ProfileName == "Threading";
        foreach (var profile in options.Profiles)
            _vm.Profiles.Add(profile);

        DataContext = _vm;
        ProfileCopy.All(options.ActiveProfile.Profile, _vm);
        _vm.SelectedProfile = options.ActiveProfile.Profile;

        ProfileCombo.SelectionChanged += OnProfileSelectionChanged;
        ProfileCombo.LostFocus += (_, _) => UpdateAddProfileEnabled();
        UpdateAddProfileEnabled();
    }

    void UpdateAddProfileEnabled()
    {
        var text = GetSelectedProfileName();
        BtnAddProfile.IsEnabled = !string.IsNullOrEmpty(text) &&
            (_vm.SelectedProfile == null || !string.Equals(_vm.SelectedProfile.Name, text, StringComparison.Ordinal));
    }

    string? GetSelectedProfileName() =>
        ProfileCombo.SelectedItem is ProfileData p ? p.Name?.Trim() : null;

    void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm.SelectedProfile != null)
            ProfileCopy.All(_vm.SelectedProfile, _vm);
        UpdateAddProfileEnabled();
    }

    void OnAddProfileClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProfile != null)
            return;

        var add = _options.Add();
        _vm.CSSMaxRPM = _vm.CSS ? _vm.CSSMaxRPM : 0.0d;
        _vm.Name = GetSelectedProfileName() ?? "Default";
        ProfileCopy.All(_vm, add);
        _vm.SelectedProfile = add;
        BtnAddProfile.IsEnabled = false;
    }

    void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProfile == null)
        {
            Close();
            return;
        }

        _vm.CSSMaxRPM = _vm.CSS ? _vm.CSSMaxRPM : 0.0d;
        ProfileCopy.All(_vm, _vm.SelectedProfile);
        _options.Update(_vm.SelectedProfile, _vm.XClearance, _vm.XMode);
        Close(true);
    }

    void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static void ShowFor(BaseViewModel model, Visual? anchor)
    {
        var owner = anchor != null
            ? TopLevel.GetTopLevel(anchor) as Window
            : null;
        owner ??= (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var dialog = new ProfileDialog(model.wz);
        if (owner != null)
            dialog.ShowDialog(owner);
        else
            dialog.Show();
    }
}

internal static class ProfileCopy
{
    public static void All<T>(T source, T target)
    {
        var type = typeof(T);
        foreach (var sourceProperty in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!sourceProperty.CanRead)
                continue;

            var targetProperty = type.GetProperty(sourceProperty.Name);
            if (targetProperty?.CanWrite != true)
                continue;

            targetProperty.SetValue(target, sourceProperty.GetValue(source));
        }
    }
}
