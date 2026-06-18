using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
using CNC.App;
using CNC.Converters;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.DragKnife;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.Views;
using CNC.Localization.Avalonia;
using ioSender.Navigation;
using ioSender.QuickAccess;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Views;
using ioSender.Workspace;

namespace ioSender;

public partial class MainWindow : Window
{
    void OnPreviewViewerRequested(object? sender, EventArgs e)
    {
        if (_viewerPreviewWindow is { IsVisible: true })
        {
            _viewerPreviewWindow.Activate();
            return;
        }

        var viewer = new RenderControl
        {
            Session = new GCodeViewerSession(
                _session.AppConfig,
                _viewModel.Grbl,
                () => _session.Program.Tokens,
                () => _session.Program.Data,
                _viewModel.SetPreviewBuilding),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        _viewerPreviewWindow = new Window
        {
            Title = "3D Viewer Preview",
            Width = 760,
            Height = 560,
            MinWidth = 360,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = viewer,
        };
        _viewerPreviewWindow.Closed += (_, _) =>
        {
            viewer.Close();
            _viewerPreviewWindow = null;
        };
        _viewerPreviewWindow.Show(this);
        Dispatcher.UIThread.Post(() => viewer.TryLoadProgramIfVisible(), DispatcherPriority.ApplicationIdle);
    }

    void UpdateFloatingPanelMenuEnabled()
    {
        var initialized = _connectionService.IsConnected && _viewModel.Grbl.IsReady;
        MnuViewSdCard.IsEnabled = initialized && GrblInfo.HasFS;
        MnuViewLatheWizards.IsEnabled = initialized && GrblInfo.LatheModeEnabled;
    }

    private void OnCameraClick(object? sender, RoutedEventArgs e)
    {
        if (_cameraWindow is { IsVisible: true })
        {
            _cameraWindow.Activate();
            return;
        }

        _cameraWindow = new CameraWindow();
        _cameraWindow.Closed += (_, _) => _cameraWindow = null;
        _cameraWindow.Show(this);
    }

    private void OnSdCardClick(object? sender, RoutedEventArgs e)
    {
        if (_sdCardWindow is { IsVisible: true })
        {
            _sdCardWindow.Activate();
            return;
        }

        _sdCardView = new SDCardView
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        _sdCardView.FileSelected += OnSdCardFileSelected;

        _sdCardWindow = CreateFloatingPanelWindow(
            Localize.T("ioSender.mainwindow.tab_sdCard", "SD Card"),
            _sdCardView,
            540,
            540);
        _sdCardWindow.Closed += (_, _) =>
        {
            if (_sdCardView is { } view)
            {
                view.FileSelected -= OnSdCardFileSelected;
                view.Activate(false);
            }

            _sdCardView = null;
            _sdCardWindow = null;
        };
        _sdCardWindow.Show(this);
        _sdCardView.Activate(true);
    }

    private void OnLatheWizardsClick(object? sender, RoutedEventArgs e)
    {
        if (_latheWizardsWindow is { IsVisible: true })
        {
            _latheWizardsWindow.Activate();
            return;
        }

        _latheWizardsView = new LatheWizardsView
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        _latheWizardsWindow = CreateFloatingPanelWindow(
            Localize.T("ioSender.mainwindow.tab_latheWizards", "Lathe Wizards"),
            _latheWizardsView,
            900,
            540);
        _latheWizardsWindow.Closed += (_, _) =>
        {
            _latheWizardsView?.Activate(false);
            _latheWizardsView = null;
            _latheWizardsWindow = null;
        };
        _latheWizardsWindow.Show(this);
        _latheWizardsView.Activate(true);
    }

    Window CreateFloatingPanelWindow(string title, Control content, double width, double height)
    {
        var window = new Window
        {
            Title = title,
            Content = content,
            Width = width,
            Height = height,
            MinWidth = Math.Min(width, 360),
            MinHeight = Math.Min(height, 300),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        PopupKeyboardService.Attach(window);
        return window;
    }

    void OnSdCardFileSelected(string filename, bool rewind)
    {
        var separator = filename.IndexOf(':');
        var selectedName = separator >= 0 ? filename[(separator + 1)..] : filename;
        var currentName = _viewModel.Grbl.FileName;
        if (currentName.StartsWith("SDCard:", StringComparison.OrdinalIgnoreCase))
            currentName = currentName["SDCard:".Length..];

        if (!string.Equals(currentName, selectedName, StringComparison.OrdinalIgnoreCase))
            _programService.Close();

        _viewModel.Grbl.FileName = filename;
        _viewModel.Grbl.SDRewind = rewind;
        NavigateTo(ShellPage.Home);
        Activate();
    }

    void CloseFloatingPanelWindows()
    {
        _sdCardWindow?.Close();
        _latheWizardsWindow?.Close();
    }
}
