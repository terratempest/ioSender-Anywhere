using Avalonia.Controls;
using CNC.App;

namespace CNC.Controls.Avalonia.Views;

public partial class JogControl : UserControl
{
    public event System.Action? HeaderStatusChanged;

    public JogControl()
    {
        InitializeComponent();
        jogBase.QueueStatusChanged += () => HeaderStatusChanged?.Invoke();
    }

    public JogControl(AppConfigService appConfig) : this()
    {
        jogBase.AppConfig = appConfig;
    }

    public string HeaderStatusText => jogBase.QueueStatusText;
    public bool IsHeaderStatusVisible => jogBase.IsQueueStatusVisible;
}
