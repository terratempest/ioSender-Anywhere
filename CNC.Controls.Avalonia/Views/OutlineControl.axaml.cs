using Avalonia.Controls;
using CNC.App;

namespace CNC.Controls.Avalonia.Views;

public partial class OutlineControl : UserControl
{
    public OutlineControl() : this(null)
    {
    }

    public OutlineControl(AppConfigService? appConfig)
    {
        InitializeComponent();
        OutlineBase.AppConfig = appConfig;
    }
}
