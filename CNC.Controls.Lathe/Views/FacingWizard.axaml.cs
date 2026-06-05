using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.Lathe;

public partial class FacingWizard : UserControl, ILatheWizardTab
{
    private bool _initOk;
    private readonly FacingLogic _logic = new();
    private readonly BaseViewModel _model;

    public FacingWizard()
    {
        InitializeComponent();
        DataContext = _model = _logic.Model;
        OutputActions.WizardModel = _model;
        OutputActions.CalculateAction = () => _logic.Calculate();
    }

    public LatheWizardType LatheWizardType => LatheWizardType.Facing;

    public void Activate(bool activate)
    {
        if (activate)
            LatheWizardActivation.OnActivate(_model, ref _initOk);
    }

}
