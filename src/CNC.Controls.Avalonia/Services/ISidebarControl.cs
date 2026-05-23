namespace CNC.Controls.Avalonia.Services;

public interface ISidebarControl
{
    string MenuLabel { get; }
    global::Avalonia.Controls.Control FlyoutRoot { get; }
    void HideFlyout();
    bool IsFlyoutVisible { get; }
}
