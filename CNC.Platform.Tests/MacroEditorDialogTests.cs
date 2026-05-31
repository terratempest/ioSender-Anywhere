using CNC.Controls.Avalonia.Views;

namespace CNC.Platform.Tests;

public class MacroEditorDialogTests
{
    [Fact]
    public void MacroEditor_exposes_async_dialog_api_only()
    {
        var showAsync = typeof(MacroEditor).GetMethod(nameof(MacroEditor.ShowAsync));

        Assert.NotNull(showAsync);
        Assert.Equal(typeof(Task<bool>), showAsync!.ReturnType);
        Assert.Null(typeof(MacroEditor).GetMethod("Show", [typeof(System.Collections.ObjectModel.ObservableCollection<CNC.GCode.Macro>), typeof(Avalonia.Controls.Window)]));
    }
}
