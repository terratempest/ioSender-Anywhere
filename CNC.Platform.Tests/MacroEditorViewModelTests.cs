using System.Collections.ObjectModel;
using CNC.Controls.Avalonia.ViewModels;
using CNC.GCode;

namespace CNC.Platform.Tests;

public class MacroEditorViewModelTests
{
    [Fact]
    public void AddMacro_uses_next_available_id()
    {
        var macros = new ObservableCollection<Macro>
        {
            new() { Id = 1, Name = "One", Code = "G0X0" },
            new() { Id = 3, Name = "Three", Code = "G0X3" },
        };
        var vm = new MacroEditorViewModel(macros);

        vm.AddMacro();

        Assert.Equal(2, vm.SelectedMacro?.Id);
        Assert.Equal("Macro 2", vm.SelectedMacro?.Name);
    }

    [Fact]
    public void CanAdd_is_false_when_all_function_key_slots_are_used()
    {
        var macros = new ObservableCollection<Macro>(
            Enumerable.Range(1, 12).Select(i => new Macro { Id = i, Name = $"M{i}", Code = "G4P0" }));

        var vm = new MacroEditorViewModel(macros);

        Assert.False(vm.CanAdd);
        vm.AddMacro();
        Assert.Equal(12, vm.Macros.Count);
    }

    [Fact]
    public void Commit_updates_macro_and_trims_trailing_newlines()
    {
        var macros = new ObservableCollection<Macro>
        {
            new() { Id = 1, Name = "Old", Code = "G0X0", ConfirmOnExecute = true },
        };
        var vm = new MacroEditorViewModel(macros);

        vm.EditorName = "New";
        vm.EditorCode = "G0X1\r\n";
        vm.EditorConfirmOnExecute = false;
        vm.Commit();

        Assert.Single(macros);
        Assert.Equal("New", macros[0].Name);
        Assert.Equal("G0X1", macros[0].Code);
        Assert.False(macros[0].ConfirmOnExecute);
    }

    [Fact]
    public void DeleteSelected_removes_macro_on_commit()
    {
        var macros = new ObservableCollection<Macro>
        {
            new() { Id = 1, Name = "One", Code = "G0X1" },
            new() { Id = 2, Name = "Two", Code = "G0X2" },
        };
        var vm = new MacroEditorViewModel(macros);

        vm.DeleteSelected();
        vm.Commit();

        Assert.Single(macros);
        Assert.Equal(2, macros[0].Id);
    }

    [Fact]
    public void Uncommitted_changes_do_not_mutate_original_collection()
    {
        var macros = new ObservableCollection<Macro>
        {
            new() { Id = 1, Name = "Original", Code = "G0X0", ConfirmOnExecute = true },
        };
        var vm = new MacroEditorViewModel(macros);

        vm.EditorName = "Edited";
        vm.EditorCode = "G0X2";
        vm.EditorConfirmOnExecute = false;

        Assert.Single(macros);
        Assert.Equal("Original", macros[0].Name);
        Assert.Equal("G0X0", macros[0].Code);
        Assert.True(macros[0].ConfirmOnExecute);
    }
}
