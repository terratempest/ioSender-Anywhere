using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe;

public partial class TurningWizard : UserControl, ILatheWizardTab
{
    private bool _initOk;
    private double _savedLength;
    private readonly TurningLogic _logic = new();
    private readonly BaseViewModel _model;

    public TurningWizard()
    {
        InitializeComponent();
        DataContext = _model = _logic.Model;
        OutputActions.WizardModel = _model;
        OutputActions.CalculateAction = () => _logic.Calculate();
        Taper.OnTaperEnabledChanged += OnTaperEnabledChanged;
        Taper.OnValueChanged += OnTaperChanged;
    }

    public LatheWizardType LatheWizardType => LatheWizardType.Turning;

    public void Activate(bool activate)
    {
        if (!activate)
            return;

        LatheWizardActivation.OnActivate(_model, ref _initOk);
    }

    private void OnTaperEnabledChanged(bool enabled)
    {
        if (enabled)
            _savedLength = LengthBox.Value;
        else
            _model.ZLength = _savedLength;

        LengthBox.IsEnabled = !enabled;
    }

    private void OnTaperChanged(double angle)
    {
        if (!Taper.IsTaperEnabled || angle == 0d || Math.Abs(_model.XStart - _model.XTarget) == 0d)
            return;

        var xtarget = _model.XTarget;
        var diameter = _model.XStart;

        if (_model.config.xmode == LatheMode.Radius)
        {
            xtarget /= 2.0d;
            diameter /= 2.0d;
        }

        _model.ZLength = (xtarget - diameter) / Math.Tan(Math.PI * angle / 180.0d) * _model.config.ZDirection;
    }

}
