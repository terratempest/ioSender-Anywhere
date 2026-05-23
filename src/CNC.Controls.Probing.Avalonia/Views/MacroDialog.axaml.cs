using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Probing;

public partial class MacroDialog : Window
{
    ProbeMacroViewModel? _vm;
    bool _loaded;

    public MacroDialog()
    {
        InitializeComponent();
    }

    public MacroDialog(ProbeMacroViewModel viewModel)
        : this()
    {
        _vm = viewModel;
        DataContext = viewModel;
        MacroCombo.SelectionChanged += (_, _) => SyncTextFromSelection();
        Opened += (_, _) =>
        {
            var vm = ViewModel;
            if (vm.SelectedMacro == null && vm.Macros.Count > 0)
                vm.SelectedMacro = vm.Macros[0];
            SyncTextFromSelection();
            _loaded = true;
        };
    }

    ProbeMacroViewModel ViewModel =>
        _vm ?? throw new InvalidOperationException("Macro dialog requires a view model.");

    void SyncTextFromSelection()
    {
        var vm = ViewModel;
        PreMacroBox.Text = vm.SelectedMacro?.PreCommands ?? string.Empty;
        PostMacroBox.Text = vm.SelectedMacro?.PostCommands ?? string.Empty;
        vm.RunOnce = vm.SelectedMacro?.RunOnce ?? false;
    }

    void OnActionMenuClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        var canEdit = vm.SelectedMacro is { Id: > 0 };
        var canAdd = vm.SelectedMacro == null;

        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "Add", IsEnabled = canAdd, Name = "mnuAdd" },
                new MenuItem { Header = "Update", IsEnabled = canEdit, Name = "mnuUpdate" },
                new MenuItem { Header = "Delete", IsEnabled = canEdit, Name = "mnuDelete" }
            }
        };

        foreach (var item in menu.Items.OfType<MenuItem>())
            item.Click += OnMenuItemClick;

        menu.Open(ActionMenuBtn);
    }

    void OnMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
            return;

        switch (item.Name)
        {
            case "mnuAdd":
                AddMacro();
                break;
            case "mnuUpdate":
                UpdateMacro();
                break;
            case "mnuDelete":
                DeleteMacro();
                break;
        }
    }

    void AddMacro()
    {
        var vm = ViewModel;
        var name = MacroCombo.SelectedItem is ProbingMacro selected
            ? selected.Name?.Trim()
            : null;
        if (string.IsNullOrEmpty(name))
            name = $"MC_{Random.Shared.Next(0, 1000)}";

        vm.Macros.Add(vm.SelectedMacro = new ProbingMacro(name, PreMacroBox.Text ?? string.Empty,
            PostMacroBox.Text ?? string.Empty, vm.RunOnce));
        SaveMacros();
        MacroCombo.SelectedItem = vm.SelectedMacro;
    }

    void UpdateMacro()
    {
        var vm = ViewModel;
        if (vm.SelectedMacro == null)
            return;

        vm.SelectedMacro.RunOnce = vm.RunOnce;
        vm.SelectedMacro.PreCommands = PreMacroBox.Text?.TrimEnd('\r', '\n') ?? string.Empty;
        vm.SelectedMacro.PostCommands = PostMacroBox.Text?.TrimEnd('\r', '\n') ?? string.Empty;
        SaveMacros();
        SyncTextFromSelection();
    }

    void DeleteMacro()
    {
        var vm = ViewModel;
        if (vm.SelectedMacro is not { Id: > 0 })
            return;

        var found = vm.Macros.FirstOrDefault(x => x.Id == vm.SelectedMacro.Id);
        if (found != null)
            vm.Macros.Remove(found);

        SaveMacros();
        vm.SelectedMacro = vm.Macros.FirstOrDefault();
        MacroCombo.SelectedItem = vm.SelectedMacro;
    }

    void SaveMacros()
    {
        var vm = ViewModel;
        var path = Core.Resources.ConfigPath + "ProbingMacros.xml";
        var xs = new System.Xml.Serialization.XmlSerializer(typeof(System.Collections.ObjectModel.ObservableCollection<ProbingMacro>));
        try
        {
            using var fs = File.Create(path);
            xs.Serialize(fs, vm.Macros);
        }
        catch (Exception ex)
        {
            GrblUi.ShowError(ex.Message, "ioSender");
        }
    }

    void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm.SelectedMacro != null && _loaded)
        {
            var changed = vm.SelectedMacro.RunOnce != vm.RunOnce ||
                          vm.SelectedMacro.PreCommands != PreMacroBox.Text ||
                          vm.SelectedMacro.PostCommands != PostMacroBox.Text;

            if (changed && GrblUi.AskYesNo(ProbingStrings.MacroChangedSave, "ioSender"))
                UpdateMacro();
            else
                SyncTextFromSelection();
        }

        vm.SaveSelectedMacro();
        Close();
    }
}
