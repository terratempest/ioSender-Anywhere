using Avalonia.Controls;

namespace CNC.Controls.Avalonia.Views;

public partial class JogControl : UserControl
{
    public event System.Action? HeaderStatusChanged;

    public JogControl()
    {
        InitializeComponent();
        jogBase.QueueStatusChanged += () => HeaderStatusChanged?.Invoke();
    }

    public string HeaderStatusText => jogBase.QueueStatusText;
    public bool IsHeaderStatusVisible => jogBase.IsQueueStatusVisible;
}
