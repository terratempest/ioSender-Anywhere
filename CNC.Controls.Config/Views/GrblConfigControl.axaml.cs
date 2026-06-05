using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CNC.Core;
using CNC.Controls.Avalonia.Utilities;

namespace CNC.Controls.Config;

public partial class GrblConfigControl : UserControl, IGrblConfigTab
{
    static readonly Action<string> NoopResponseHandler = _ => { };

    bool _active;
    GrblViewModel? _model;
    GrblSettingDetails? _selected;

    public GrblConfigControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => EnsureModel();
    }

    public GrblConfigType GrblConfigType => GrblConfigType.Base;

    public void Activate(bool activate)
    {
        if (activate)
        {
            if (_active)
                return;

            _active = true;
            if (!EnsureModel())
                return;

            _model!.Message = string.Empty;
            btnSave.IsEnabled = !_model.IsCheckMode;
            if (!IsConnected())
            {
                _model.Message = "Connect to the controller to load Grbl settings.";
                UpdateButtons();
                return;
            }

            ReloadFromController();
        }
        else
        {
            _active = false;
            if (_model == null)
                return;

            CommitValueEdit();
            if (GrblSettings.HasChanges() &&
                GrblUi.AskYesNo("Settings changed, save now?", "ioSender"))
                GrblSettings.Save();
        }
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureModel();
        ApplyViewMode();

        if (_active && _model != null)
        {
            btnSave.IsEnabled = !_model.IsCheckMode;
            if (IsConnected())
                ReloadFromController();
            else
                _model.Message = "Connect to the controller to load Grbl settings.";
        }

        UpdateButtons();
    }

    bool EnsureModel()
    {
        if (_model != null)
            return true;

        _model = DataContext as GrblViewModel;
        return _model != null;
    }

    void ApplyViewMode()
    {
        var useTree = GrblInfo.HasEnums;
        dgrSettings.IsVisible = !useTree;
        treeView.IsVisible = useTree;
        searchField.IsVisible = useTree;
        details.IsVisible = !useTree || _selected != null;

        if (useTree)
            treeView.ItemsSource = GrblSettingGroups.Groups;
        else
        {
            dgrSettings.ItemsSource = GrblSettings.Settings;
            if (GrblSettings.Settings.Count > 0 && dgrSettings.SelectedIndex < 0)
                dgrSettings.SelectedIndex = 0;
        }
    }

    void ReloadFromController()
    {
        GrblSettings.Load();
        RefreshSelectedSetting();
        UpdateButtons();
    }

    void RefreshSelectedSetting()
    {
        if (treeView.SelectedItem is GrblSettingDetails treeSetting)
            ShowSetting(treeSetting, assign: false);
        else if (dgrSettings.SelectedItem is GrblSettingDetails gridSetting)
            ShowSetting(gridSetting, assign: false);
        else if (_selected != null)
            ShowSetting(_selected, assign: false);
        else
            details.IsVisible = !GrblInfo.HasEnums;
    }

    void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = e;
    }

    void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is GrblSettingDetails setting)
            ShowSetting(setting, assign: true);
    }

    void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is GrblSettingDetails setting && setting.Value != null)
            ShowSetting(setting, assign: true);
        else
            details.IsVisible = false;
    }

    void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Source is not Control source)
            return;

        if (source.GetVisualAncestors().OfType<ToggleButton>().Any())
            return;

        var item = source as TreeViewItem ??
                   source.GetVisualAncestors().OfType<TreeViewItem>().FirstOrDefault();

        if (item?.DataContext is not GrblSettingGroup)
            return;

        item.IsExpanded = !item.IsExpanded;
        e.Handled = true;
    }

    void ShowSetting(GrblSettingDetails setting, bool assign)
    {
        details.IsVisible = true;
        if (_selected != null && assign)
            CommitValueEdit();

        _selected = setting;
        searchField.Value = setting.Id;
        txtSettingTitle.Text = $"${setting.Id} — {setting.Name}";
        txtValue.Text = setting.Value ?? string.Empty;
        BuildValueEditor(setting);
        txtDescription.Text = setting.Description;
        UpdateButtons();
    }

    void CommitValueEdit()
    {
        if (_selected == null)
            return;

        var text = txtValue.IsVisible
            ? txtValue.Text ?? string.Empty
            : GetValueEditorText(_selected);

        if (text != (_selected.Value ?? string.Empty))
            _selected.Value = text;
    }

    void OnValueLostFocus(object? sender, FocusChangedEventArgs e) => CommitValueEdit();

    void BuildValueEditor(GrblSettingDetails setting)
    {
        valueEditorPanel.Children.Clear();
        valueEditorPanel.IsVisible = false;
        txtValue.IsVisible = true;

        switch (setting.DataType)
        {
            case GrblSettingDetails.DataTypes.BOOL:
                txtValue.IsVisible = false;
                valueEditorPanel.IsVisible = true;
                valueEditorPanel.Children.Add(new CheckBox
                {
                    Content = "Enabled",
                    FontSize = 12,
                    IsChecked = ParseSettingInt(setting.Value) != 0
                });
                break;

            case GrblSettingDetails.DataTypes.BITFIELD:
            case GrblSettingDetails.DataTypes.XBITFIELD:
            case GrblSettingDetails.DataTypes.AXISMASK:
                txtValue.IsVisible = false;
                valueEditorPanel.IsVisible = true;
                AddBitfieldEditor(setting);
                break;

            case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                txtValue.IsVisible = false;
                valueEditorPanel.IsVisible = true;
                AddRadioEditor(setting);
                break;
        }
    }

    string GetValueEditorText(GrblSettingDetails setting)
    {
        switch (setting.DataType)
        {
            case GrblSettingDetails.DataTypes.BOOL:
                return valueEditorPanel.Children.OfType<CheckBox>().FirstOrDefault()?.IsChecked == true ? "1" : "0";

            case GrblSettingDetails.DataTypes.BITFIELD:
            case GrblSettingDetails.DataTypes.XBITFIELD:
            case GrblSettingDetails.DataTypes.AXISMASK:
                var value = 0;
                foreach (var checkBox in valueEditorPanel.Children.OfType<CheckBox>())
                {
                    if (checkBox.Tag is int bit && checkBox.IsChecked == true)
                        value |= 1 << bit;
                }
                return value.ToString();

            case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                return valueEditorPanel.Children.OfType<RadioButton>()
                    .Where(radio => radio.IsChecked == true)
                    .Select(radio => radio.Tag?.ToString() ?? setting.Value ?? string.Empty)
                    .FirstOrDefault() ?? setting.Value ?? string.Empty;
        }

        return txtValue.Text ?? string.Empty;
    }

    void AddBitfieldEditor(GrblSettingDetails setting)
    {
        var value = ParseSettingInt(setting.Value);
        var labels = GetBitLabels(setting, value);

        for (var bit = 0; bit < labels.Count; bit++)
        {
            valueEditorPanel.Children.Add(new CheckBox
            {
                Content = labels[bit],
                FontSize = 12,
                Tag = bit,
                IsChecked = (value & (1 << bit)) != 0
            });
        }
    }

    void AddRadioEditor(GrblSettingDetails setting)
    {
        var selected = ParseSettingInt(setting.Value);
        var options = SplitSettingFormat(setting.Format);
        if (options.Count == 0)
            options.Add(setting.Value ?? "0");

        var groupName = $"setting{setting.Id}";
        for (var index = 0; index < options.Count; index++)
        {
            valueEditorPanel.Children.Add(new RadioButton
            {
                Content = options[index],
                FontSize = 12,
                GroupName = groupName,
                Tag = index,
                IsChecked = index == selected
            });
        }
    }

    static List<string> GetBitLabels(GrblSettingDetails setting, int value)
    {
        if (setting.DataType == GrblSettingDetails.DataTypes.AXISMASK)
        {
            var axes = Math.Max(GrblInfo.NumAxes, HighestBit(value) + 1);
            return Enumerable.Range(0, axes)
                .Select(GrblInfo.AxisIndexToLetter)
                .ToList();
        }

        var labels = SplitSettingFormat(setting.Format);
        var count = Math.Max(labels.Count, HighestBit(value) + 1);
        if (count == 0)
            count = 8;

        while (labels.Count < count)
            labels.Add($"Bit {labels.Count}");

        return labels;
    }

    static List<string> SplitSettingFormat(string format) =>
        format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    static int ParseSettingInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;

    static int HighestBit(int value)
    {
        var bit = -1;
        while (value != 0)
        {
            bit++;
            value >>= 1;
        }

        return bit;
    }

    void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (!EnsureConnected() || _model is not { } model)
            return;

        model.Message = string.Empty;
        ReloadFromController();
    }

    void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!EnsureConnected() || _model is not { } model)
            return;

        CommitValueEdit();
        model.Message = string.Empty;
        GrblSettings.Save();
        ReloadFromController();
    }

    void OnBackupClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not { } model)
            return;

        var path = Path.Combine(Core.Resources.ConfigPath, "settings.txt");
        if (GrblSettings.Backup(path))
            model.Message = "All settings written to settings.txt in the sender folder.";
        GrblWorkParameters.Backup(Path.Combine(Core.Resources.ConfigPath, "offsets.nc"));
    }

    async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Restore settings from file",
            AllowMultiple = false,
            SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(Core.Resources.ConfigPath),
            FileTypeFilter =
            [
                new FilePickerFileType("Text files") { Patterns = ["*.txt"] }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            LoadFile(path);
    }

    void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var setting = GrblSettings.Get((GrblSetting)(int)searchField.Value);
        if (setting == null)
            return;

        foreach (var group in GrblSettingGroups.Groups)
        {
            if (group.Id != setting.GroupId)
                continue;

            foreach (var item in group.Settings)
            {
                if (item.Id != setting.Id)
                    continue;
                treeView.SelectedItem = item;
                treeView.ScrollIntoView(item);
                return;
            }
        }
    }

    void UpdateButtons()
    {
        var connected = IsConnected();
        btnReload.IsEnabled = connected;
        btnSave.IsEnabled = connected && _model is { IsCheckMode: false };
        btnBackup.IsEnabled = GrblSettings.IsLoaded;
        btnRestore.IsEnabled = connected;
    }

    bool IsConnected() => Comms.com?.IsOpen == true;

    bool EnsureConnected()
    {
        if (IsConnected())
            return true;

        if (_model is { } model)
            model.Message = "Not connected.";
        return false;
    }

    bool LoadFile(string filename)
    {
        if (_model is not { } model)
            return false;

        var settings = new Dictionary<int, string>();
        var lines = new List<string>();
        using (var sr = File.OpenText(filename))
        {
            string? block = sr.ReadLine();
            while (block != null)
            {
                block = block.Trim();
                try
                {
                    if (lines.Count == 0 && model.IsGrblHAL && block == "%")
                        lines.Add(block);
                    else if (block.StartsWith('$') && block.IndexOf('=') is int pos and > 1 &&
                             int.TryParse(block.Substring(1, pos - 1), out var id))
                        settings[id] = block.Substring(pos + 1);
                    else
                        lines.Add(block);
                    block = sr.ReadLine();
                }
                catch (Exception ex)
                {
                    if (!GrblUi.AskYesNo($"Bummer...\r\rContinue loading?\r\r{ex.Message}", "ioSender"))
                    {
                        settings.Clear();
                        lines.Clear();
                        break;
                    }
                    block = sr.ReadLine();
                }
            }
        }

        if (settings.Count == 0)
        {
            GrblUi.ShowError("The file does not contain any settings.", "ioSender");
            return false;
        }

        var dep = new List<int> { (int)GrblSetting.HomingEnable };
        foreach (var cmd in lines)
        {
            if (!WriteSettingCommand(cmd))
                break;
        }

        foreach (var d in dep)
        {
            if (!settings.TryGetValue(d, out var val))
                continue;
            if (!GrblSettings.HasSetting((GrblSetting)d))
                continue;
            if (!SetSetting(new KeyValuePair<int, string>(d, val)))
            {
                settings.Clear();
                break;
            }
        }

        var mismatch = 0;
        foreach (var setting in settings)
        {
            if (!GrblSettings.HasSetting((GrblSetting)setting.Key))
            {
                mismatch++;
                continue;
            }

            if (dep.Contains(setting.Key))
                continue;

            if (!SetSetting(setting))
                break;
        }

        if (lines.Count > 0 && lines[0] == "%" && Comms.com is { } comms)
            comms.WriteCommand("%");

        ReloadFromController();
        model.Message = string.Empty;

        if (mismatch > 0)
            GrblUi.ShowError($"{mismatch} settings were ignored, not supported by the controller.", "ioSender");

        return settings.Count > 0;
    }

    bool SetSetting(KeyValuePair<int, string> setting)
    {
        var scmd = $"${setting.Key}={setting.Value}";
        return WriteSettingCommand(scmd, setting.Key);
    }

    bool WriteSettingCommand(string scmd, int? settingId = null)
    {
        if (_model is not { } model || Comms.com is not { } comms)
            return false;

        var retval = string.Empty;
        bool? res = null;
        var cancellationToken = new CancellationToken();

        new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                cancellationToken,
                response =>
                {
                    if (response != "ok")
                        retval = response ?? string.Empty;
                },
                a => model.OnResponseReceived += a,
                a => RemoveResponseHandler(model, a),
                400,
                () => comms.WriteCommand(scmd));
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        if (retval != string.Empty)
        {
            if (retval.StartsWith("error:"))
            {
                var msg = GrblErrors.GetMessage(retval.Substring(6));
                if (msg != retval)
                    retval += " - \"" + msg + "\"";
            }

            var details = settingId.HasValue ? GrblSettings.Get((GrblSetting)settingId.Value) : null;
            var title = "ioSender" + (details == null ? "" : " - " + details.Name);
            return GrblUi.AskYesNo($"Setting {scmd} returned {retval}, continue?", title);
        }

        if (res == false)
            return GrblUi.AskYesNo($"Timed out while setting {scmd}, continue?", "ioSender");

        return true;
    }

    static void RemoveResponseHandler(GrblViewModel model, Action<string> handler)
    {
        model.OnResponseReceived = (Action<string>?)Delegate.Remove(model.OnResponseReceived, handler) ?? NoopResponseHandler;
    }
}
