using Avalonia.Controls;
using CNC.Platform.Abstractions;

namespace ioSender.Views;

/// <summary>Machine view host; layout is provided by <see cref="Workspace.Controls.WorkspaceHost"/>.</summary>
public partial class JobView : UserControl
{
    public JobView() => InitializeComponent();

    public Workspace.Controls.WorkspaceHost WorkspaceHost => Workspace;

    public void SetLayoutReady(bool ready)
    {
        Workspace.IsLayoutReady = ready;
        if (ready && DataContext is not null)
            Workspace.Rebuild();
    }
}
