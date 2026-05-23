using Avalonia.Controls;

using Avalonia.Interactivity;

using CNC.App;

using CNC.Core;



namespace CNC.Controls.Config;



public partial class AppCameraConfigPanel : UserControl

{

    public AppCameraConfigPanel() => InitializeComponent();



    void OnGetPositionClick(object? sender, RoutedEventArgs e)

    {

        if (DataContext is not BaseConfig cfg)

            return;



        var grbl = Grbl.GrblViewModel;

        if (grbl == null)

        {

            GetPositionBtn.IsEnabled = false;

            return;

        }



        cfg.Camera.XOffset = -grbl.Position.X;

        cfg.Camera.YOffset = -grbl.Position.Y;

    }

}

