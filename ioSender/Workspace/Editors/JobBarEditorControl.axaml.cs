using Avalonia.Controls;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;

namespace ioSender.Workspace.Editors;

public partial class JobBarEditorControl : UserControl
{
    public JobBarEditorControl() : this(null, null)
    {
    }

    public JobBarEditorControl(BaseConfig? appBase, MachineCommandService? commands = null)
    {
        InitializeComponent();
        JobHost.Content = new JobControl(appBase, commands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
    }
}
