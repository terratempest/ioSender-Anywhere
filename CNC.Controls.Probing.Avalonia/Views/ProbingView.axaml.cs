using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using CNC.App;
using CNC.Core;
using CNC.Core.Input;

namespace CNC.Controls.Probing;

public partial class ProbingView : UserControl, IKeyHandlerContext
{
    string IKeyHandlerContext.Name => "Probing";

    object? IKeyHandlerContext.DataContext => DataContext;
    readonly ProbingViewModel _vm;
    bool _active;
    bool _keyboardMapped;
    bool _cycleStartSignal;

    public ProbingView() : this(null)
    {
    }

    public ProbingView(BaseConfig? config)
    {
        _vm = new ProbingViewModel(config);
        InitializeComponent();
        SidebarHost.DataContext = _vm;
        WizardTabs.DataContext = _vm;
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DeactivateProbing();
    }

    public void Activate(bool activate)
    {
        if (DataContext is not GrblViewModel grbl)
            return;

        if (activate == _active)
            return;

        if (activate)
            ActivateProbing(grbl);
        else
            DeactivateProbing();
    }

    void ActivateProbing(GrblViewModel grbl)
    {
        _active = true;
        UpdateConnectionOverlay();
        grbl.IsProbing = true;

        if (!_keyboardMapped)
        {
            grbl.Keyboard.AddHandler(Key.R, ModifierKeys.Alt, _ => StartActiveTab(), this);
            grbl.Keyboard.AddHandler(Key.S, ModifierKeys.Alt, _ => StopActiveTab(), this);
            _keyboardMapped = true;
        }

        _vm.Attach(grbl);
        _vm.OnActivated();

        if (GrblInfo.IsGrblHAL && Comms.com != null)
            Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

        grbl.PropertyChanged += Grbl_PropertyChanged;
        grbl.IgnoreNextCycleStart = true;

        _cycleStartSignal = grbl.Signals.Value.HasFlag(Signals.CycleStart);
        ActivateSelectedTab(true);
    }

    void DeactivateProbing()
    {
        if (!_active)
            return;

        if (DataContext is GrblViewModel grbl)
        {
            grbl.PropertyChanged -= Grbl_PropertyChanged;
            grbl.IsProbing = false;
        }

        ActivateSelectedTab(false);
        _vm.OnDeactivated();
        _vm.Detach();
        _active = false;
        UpdateConnectionOverlay();
    }

    void UpdateConnectionOverlay()
    {
        var connected = Comms.com is { IsOpen: true };
        DisconnectedOverlay.IsVisible = _active && !connected;
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is GrblViewModel grbl && _active)
        {
            _vm.Attach(grbl);
            _vm.OnActivated();
        }
    }

    void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Equals(e.Source, sender) || e.RemovedItems.Count == 0 && e.AddedItems.Count == 0)
            return;

        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removed)
            GetProbeTab(removed)?.Activate(false);

        if (e.AddedItems.Count == 1 && e.AddedItems[0] is TabItem added)
        {
            var view = GetProbeTab(added);
            if (view != null)
            {
                _vm.ProbingType = view.ProbingType;
                _vm.Positions.Clear();
                _vm.Message = string.Empty;
                view.Activate(true);
            }
        }

        e.Handled = true;
    }

    void Grbl_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel grbl)
            return;

        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.IsJobRunning):
                foreach (var item in WizardTabs.Items.OfType<TabItem>())
                    item.IsEnabled = !grbl.IsJobRunning || item == WizardTabs.SelectedItem;
                break;

            case nameof(GrblViewModel.Signals):
                if (!grbl.IsJobRunning)
                {
                    var signals = grbl.Signals.Value;
                    if (signals.HasFlag(Signals.CycleStart) && !signals.HasFlag(Signals.Hold) && !_cycleStartSignal)
                        StartActiveTab();
                    _cycleStartSignal = signals.HasFlag(Signals.CycleStart);
                }
                break;
        }
    }

    static IProbeTab? GetProbeTab(TabItem tab)
    {
        foreach (var child in tab.GetVisualDescendants().OfType<Control>())
        {
            if (child is IProbeTab probeTab)
                return probeTab;
        }

        return null;
    }

    IProbeTab? GetSelectedProbeTab() =>
        WizardTabs.SelectedItem is TabItem tab ? GetProbeTab(tab) : null;

    void ActivateSelectedTab(bool activate) => GetSelectedProbeTab()?.Activate(activate);

    bool StartActiveTab()
    {
        if (DataContext is GrblViewModel grbl && !grbl.IsJobRunning)
            GetSelectedProbeTab()?.Start();
        return true;
    }

    bool StopActiveTab()
    {
        GetSelectedProbeTab()?.Stop();
        return true;
    }

    void OnProfileMenuClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "Add", Name = "mnuAdd" },
                new MenuItem { Header = "Update", Name = "mnuUpdate" },
                new MenuItem { Header = "Delete", Name = "mnuDelete" }
            }
        };

        foreach (var item in menu.Items.OfType<MenuItem>())
            item.Click += OnProfileMenuItemClick;

        menu.Open(ProfileMenuBtn);
    }

    void OnMacroButtonClick(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window
            ?? (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var dialog = new MacroDialog(_vm.Macro);
        if (owner != null)
            dialog.ShowDialog(owner);
        else
            dialog.Show();
    }

    void OnProfileMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
            return;

        var name = ProfileCombo.SelectedItem is ProbingProfile profile
            ? profile.Name?.Trim()
            : null;
        if (string.IsNullOrEmpty(name))
            name = "<Default>";

        switch (item.Name)
        {
            case "mnuAdd":
                var id = _vm.ProfileStore.Add(name, _vm);
                _vm.Profile = _vm.Profiles.FirstOrDefault(p => p.Id == id);
                break;

            case "mnuUpdate":
                if (_vm.Profile != null)
                    _vm.ProfileStore.Update(_vm.Profile.Id, name, _vm);
                break;

            case "mnuDelete":
                if (_vm.Profile != null && _vm.ProfileStore.Delete(_vm.Profile.Id))
                    _vm.Profile = _vm.Profiles.FirstOrDefault();
                break;
        }

        _vm.ProfileStore.Save();
    }
}
