using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.Lathe;

public partial class PartingWizard : UserControl, ILatheWizardTab
{
    private bool _initOk;
    private readonly PartingLogic _logic = new();
    private readonly BaseViewModel _model;

    public PartingWizard()
    {
        InitializeComponent();
        DataContext = _model = _logic.Model;
        OutputActions.WizardModel = _model;
        OutputActions.CalculateAction = () => _logic.Calculate();
    }

    public LatheWizardType LatheWizardType => LatheWizardType.Parting;

    public void Activate(bool activate)
    {
        if (activate)
            LatheWizardActivation.OnActivate(_model, ref _initOk);
    }

}
