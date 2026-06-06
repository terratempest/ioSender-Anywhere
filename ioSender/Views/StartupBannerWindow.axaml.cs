using Avalonia.Controls;
using ioSender.Services;

namespace ioSender.Views;

public partial class StartupBannerWindow : Window
{
    public StartupBannerWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = "Version: " + AppVersion.DisplayVersion;
    }

    public void ReportProgress(string statusText, int percent)
    {
        StatusTextBlock.Text = statusText;
        StartupProgress.Value = Math.Clamp(percent, 0, 100);
    }
}
