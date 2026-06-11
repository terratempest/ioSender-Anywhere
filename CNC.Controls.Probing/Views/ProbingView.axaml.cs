using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using CNC.App;
using CNC.Controls.Avalonia.Utilities;
using CNC.Core;
using CNC.Core.Input;
using CoreKey = CNC.Core.Input.Key;

namespace CNC.Controls.Probing;

public partial class ProbingView : UserControl, IKeyHandlerContext
{
    string IKeyHandlerContext.Name => "Probing";

    object? IKeyHandlerContext.DataContext => DataContext;
    readonly ProbingViewModel _vm;
    bool _initialized;
    TabItem? _activeWizardTab;
    bool _active;
    bool _keyboardMapped;
    bool _jogEnabled;
    bool _cycleStartSignal;
    GrblViewModel? _activeGrbl;
    Button[]? _wizardActionButtons;

    public ProbingView() : this(null)
    {
    }

    public ProbingView(BaseConfig? config)
    {
        _vm = new ProbingViewModel(config);
        InitializeComponent();
        ProbingVmRoot.DataContext = _vm;
        _wizardActionButtons =
        [
            ToolLengthActionButton,
            EdgeExternalActionButton,
            EdgeInternalActionButton,
            RotationActionButton,
            CenterActionButton,
            HeightMapActionButton
        ];
        _initialized = true;
        WizardTabs.PropertyChanged += OnWizardTabsPropertyChanged;
        CommitWizardTabChange(null, WizardTabs.SelectedItem as TabItem);
        UpdateWizardActionButtons();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DeactivateProbing();
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
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
        _activeGrbl = grbl;
        UpdateConnectionOverlay();
        grbl.IsProbing = true;

        if (!_keyboardMapped)
        {
            grbl.Keyboard.AddHandler(CoreKey.R, ModifierKeys.Alt, _ => StartActiveTab(), this);
            grbl.Keyboard.AddHandler(CoreKey.S, ModifierKeys.Alt, _ => StopActiveTab(), this);
            grbl.Keyboard.AddHandler(CoreKey.C, ModifierKeys.Alt, _ => ProbeConnectedToggle(), this);
            _keyboardMapped = true;
        }

        _vm.Attach(grbl);
        SyncProfileName();
        _vm.OnActivated();
        grbl.OnCameraProbe += AddCameraPosition;

        if (GrblInfo.IsGrblHAL && Comms.com != null)
            Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

        grbl.PropertyChanged += Grbl_PropertyChanged;
        grbl.IgnoreNextCycleStart = true;

        _cycleStartSignal = grbl.Signals.Value.HasFlag(Signals.CycleStart);
        ActivateSelectedTab(true);
        SyncSidebarFromSelectedTab();
    }

    void DeactivateProbing()
    {
        if (!_active)
            return;

        if (_activeGrbl is { } grbl)
        {
            grbl.PropertyChanged -= Grbl_PropertyChanged;
            grbl.OnCameraProbe -= AddCameraPosition;
            grbl.IsProbing = false;
        }

        ActivateSelectedTab(false);
        _vm.OnDeactivated();
        _vm.Detach();
        _activeGrbl = null;
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
        if (!_active || DataContext is not GrblViewModel grbl || ReferenceEquals(grbl, _activeGrbl))
            return;

        ActivateSelectedTab(false);

        if (_activeGrbl is { } oldGrbl)
        {
            oldGrbl.PropertyChanged -= Grbl_PropertyChanged;
            oldGrbl.OnCameraProbe -= AddCameraPosition;
            oldGrbl.IsProbing = false;
            _vm.OnDeactivated();
        }

        _activeGrbl = grbl;
        grbl.IsProbing = true;
        _vm.Attach(grbl);
        SyncProfileName();
        _vm.OnActivated();
        grbl.OnCameraProbe += AddCameraPosition;
        grbl.PropertyChanged += Grbl_PropertyChanged;
        grbl.IgnoreNextCycleStart = true;
        _cycleStartSignal = grbl.Signals.Value.HasFlag(Signals.CycleStart);
        ActivateSelectedTab(true);
        SyncSidebarFromSelectedTab();
        UpdateConnectionOverlay();
    }

    void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || WizardTabs == null || !ReferenceEquals(sender, WizardTabs))
            return;

        if (e.RemovedItems.Count == 0 && e.AddedItems.Count == 0)
            return;

        var removed = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as TabItem : null;
        var added = e.AddedItems.Count > 0 ? e.AddedItems[0] as TabItem : WizardTabs.SelectedItem as TabItem;
        CommitWizardTabChange(removed, added);
        e.Handled = true;
    }

    void OnWizardTabsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_initialized || e.Property != SelectingItemsControl.SelectedItemProperty)
            return;

        var added = e.NewValue as TabItem;
        if (ReferenceEquals(added, _activeWizardTab))
            return;

        CommitWizardTabChange(_activeWizardTab, added);
    }

    void CommitWizardTabChange(TabItem? removed, TabItem? added)
    {
        if (added == null)
            added = WizardTabs?.SelectedItem as TabItem;

        if (added == null)
            return;

        if (ReferenceEquals(added, _activeWizardTab) && removed == null)
        {
            _vm.NotifySidebarVisibility();
            return;
        }

        if (removed != null && !ReferenceEquals(removed, added))
        {
            var removedTab = GetProbeTab(removed);
            if (removedTab != null)
            {
                if (_active)
                    removedTab.Activate(false);
                _vm.OnProbeTabActivated(removedTab.ProbingType, false);
            }
        }

        var view = GetProbeTab(added);
        if (view == null)
            return;

        ApplySelectedWizardTab(added, view);
    }

    void ApplySelectedWizardTab(TabItem tab, IProbeTab view)
    {
        _activeWizardTab = tab;
        var allowsMeasure = view.ProbingType is ProbingType.EdgeFinderExternal or ProbingType.EdgeFinderInternal or ProbingType.CenterFinder;
        if (!allowsMeasure && _vm.CoordinateMode == ProbingViewModel.CoordMode.Measure)
            _vm.CoordinateMode = ProbingViewModel.CoordMode.G10;

        _vm.AllowMeasure = false;
        _vm.ProbingType = view.ProbingType;
        _vm.Positions.Clear();
        _vm.Message = string.Empty;
        _vm.PreviewEnable = false;
        _vm.RestoreProfileForTab(view.ProbingType);
        SyncProfileName();
        _vm.OnProbeTabActivated(view.ProbingType, true);
        if (_active)
            view.Activate(true);
        _vm.NotifySidebarVisibility();
        UpdateWizardActionButtons();
    }

    void SyncSidebarFromSelectedTab()
    {
        var tab = GetSelectedProbeTab();
        if (tab == null)
            return;

        var type = tab.ProbingType;
        if (_vm.ProbingType != type)
            _vm.ProbingType = type;
        else
            _vm.NotifySidebarVisibility();
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
                UpdateWizardActionButtons();
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
        if (tab.Content is IProbeTab contentProbeTab)
            return contentProbeTab;

        if (tab.Content is Control contentControl)
        {
            foreach (var child in contentControl.GetVisualDescendants().OfType<Control>())
            {
                if (child is IProbeTab probeTab)
                    return probeTab;
            }
        }

        foreach (var child in tab.GetVisualDescendants().OfType<Control>())
        {
            if (child is IProbeTab probeTab)
                return probeTab;
        }

        return null;
    }

    IProbeTab? GetSelectedProbeTab()
    {
        if (WizardTabs?.SelectedItem is not TabItem tab)
            return null;

        return GetProbeTab(tab);
    }

    void ActivateSelectedTab(bool activate)
    {
        var tab = GetSelectedProbeTab();
        if (tab == null)
            return;

        if (activate)
        {
            _vm.RestoreProfileForTab(tab.ProbingType);
            SyncProfileName();
        }

        _vm.OnProbeTabActivated(tab.ProbingType, activate);
        tab.Activate(activate);
    }

    bool StartActiveTab()
    {
        if (DataContext is GrblViewModel grbl && !grbl.IsJobRunning)
        {
            var tab = GetSelectedProbeTab();
            if (tab != null)
            {
                tab.Start(_vm.PreviewEnable);
            }
        }
        return true;
    }

    bool StopActiveTab()
    {
        GetSelectedProbeTab()?.Stop();
        return true;
    }

    bool ProbeConnectedToggle()
    {
        if (Comms.com is { IsOpen: true })
            Comms.com.WriteByte(GrblConstants.CMD_PROBE_CONNECTED_TOGGLE);
        return true;
    }

    void AddCameraPosition(Position position)
    {
        if (DataContext is not GrblViewModel { IsProbing: true })
            return;

        if (_vm.CameraPositions == 0)
        {
            _vm.PreviewText = string.Empty;
            _vm.PreviewEnable = true;
        }

        _vm.Positions.Add(position);
        var count = _vm.Positions.Count;
        _vm.CameraPositions = count;
        if (count == _vm.CameraPositions)
        {
            var line = string.Format(
                "Camera position {0}, X: {1}, Y: {2}",
                count,
                position.X.ToInvariantString(),
                position.Y.ToInvariantString());
            _vm.PreviewText += string.IsNullOrEmpty(_vm.PreviewText) ? line : "\n" + line;
        }

        JogButton.Focus();
    }

    void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ProcessKeyPreview(e, isUp: false))
            e.Handled = true;
    }

    void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (ProcessKeyPreview(e, isUp: true))
            e.Handled = true;
    }

    bool ProcessKeyPreview(KeyEventArgs e, bool isUp)
    {
        if (DataContext is not GrblViewModel grbl)
            return false;

        var info = AvaloniaKeyBridge.ToKeyEventInfo(e, isUp);
        var allowJog = e.KeyModifiers == (global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift) || _jogEnabled;
        return grbl.Keyboard.ProcessKeypress(info, allowJog, this);
    }

    void OnJogGotFocus(object? sender, FocusChangedEventArgs e) => SetJogFocus(true);

    void OnJogLostFocus(object? sender, FocusChangedEventArgs e) => SetJogFocus(false);

    void SetJogFocus(bool focused)
    {
        if (DataContext is not GrblViewModel grbl)
            return;

        if (grbl.Keyboard.IsJogging)
            grbl.Keyboard.JogCancel();

        _jogEnabled = focused && grbl.Keyboard.CanJog2;
        JogButton.Content = _jogEnabled ? "Keyboard jogging active" : "Keyboard jogging disabled";
    }

    void OnProbeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: true } && e.AddedItems.Count == 1 && e.AddedItems[0] is Probe probe && DataContext is GrblViewModel grbl)
            grbl.ExecuteCommand(string.Format(GrblCommand.ProbeSelect, probe.Id));
    }

    void OnProfileMenuClick(object? sender, RoutedEventArgs e)
    {
        var selectedName = _vm.Profile?.Name?.Trim();
        var typedName = ProfileNameBox.Text?.Trim();
        var isSameName = !string.IsNullOrEmpty(typedName) && string.Equals(selectedName, typedName, StringComparison.Ordinal);
        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "Add", Name = "mnuAdd", IsEnabled = !isSameName && !string.IsNullOrWhiteSpace(typedName) },
                new MenuItem { Header = "Update", Name = "mnuUpdate", IsEnabled = isSameName && _vm.Profile != null },
                new MenuItem { Header = "Delete", Name = "mnuDelete", IsEnabled = isSameName && _vm.Profiles.Count > 1 }
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

    void OnWizardActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag } || !int.TryParse(tag.ToString(), out var index))
            return;

        if (index >= 0 && index < WizardTabs.ItemCount)
            WizardTabs.SelectedIndex = index;
    }

    void UpdateWizardActionButtons()
    {
        if (_wizardActionButtons == null)
            return;

        var selectedIndex = WizardTabs.SelectedIndex;
        var isJobRunning = DataContext is GrblViewModel { IsJobRunning: true };

        for (var i = 0; i < _wizardActionButtons.Length; i++)
        {
            var button = _wizardActionButtons[i];
            var isSelected = i == selectedIndex;
            button.Classes.Set("selected", isSelected);
            button.IsEnabled = !isJobRunning || isSelected;
        }
    }

    void OnProfileMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
            return;

        var name = ProfileNameBox.Text?.Trim();
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
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = _vm.Profiles;
        RememberSelectedProfileForActiveTab();
        SyncProfileName();
    }

    void OnProfileDropDownClick(object? sender, RoutedEventArgs e) =>
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;

    void OnProfileListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProbingProfile profile)
            return;

        _vm.Profile = profile;
        RememberSelectedProfileForActiveTab();
        ProfileNameBox.Text = profile.Name ?? string.Empty;
        ProfilePopup.IsOpen = false;
        ProfileList.SelectedItem = null;
    }

    void RememberSelectedProfileForActiveTab()
    {
        var tab = GetSelectedProbeTab();
        if (tab != null)
            _vm.RememberProfileForTab(tab.ProbingType);
    }

    void SyncProfileName() =>
        ProfileNameBox.Text = _vm.Profile?.Name ?? string.Empty;
}
