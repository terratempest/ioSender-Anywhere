using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class About : Window
{
    private const string GitHubRepositoryUrl = "https://github.com/terratempest/ioSender-crossplatform";

    private readonly string _baseTitle;
    private readonly string _connection;

    public About() : this("ioSender", string.Empty)
    {
    }

    public About(string title, string connection)
    {
        InitializeComponent();
        _baseTitle = title;
        _connection = connection;
        ApplyLocalization();
        Opened += OnOpened;
    }

    private void ApplyLocalization()
    {
        Localize.Set(AboutRoot, "CNC.Controls.Avalonia.about.dlg_about", "About");
        Localize.Apply(BtnOk);
        Localize.Apply(BtnToClipboard);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Title = CNC.Core.Resources.IsLegacyController
            ? _baseTitle + " (legacy mode)"
            : _baseTitle;

        if (DataContext is GrblViewModel { IsMPGActive: not true })
            GrblInfo.Get();

        TxtGrblVersion.Text = GrblInfo.Version;
        TxtGrblOptions.Text = GrblInfo.Options;
        TxtGrblNewOptions.Text = GrblInfo.NewOptions;
        TxtGrblConnection.Text = _connection;
        TxtSystemInfo.Text = string.Join(Environment.NewLine, GrblInfo.SystemInfo);
        TxtAppVersion.Text = "Version " + GetAppVersion();

        var header = GrblInfo.Firmware;
        if (!string.IsNullOrEmpty(GrblInfo.Identity))
            header += ": " + GrblInfo.Identity;
        GrpGrbl.Header = header;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();

    private void OnToClipboardClick(object? sender, RoutedEventArgs e) => GrblSettings.CopyToClipboard();

    private static string GetAppVersion()
    {
        var assembly = typeof(About).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "-";
    }

    private static void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(GitHubRepositoryUrl)
        {
            UseShellExecute = true
        });
    }
}
