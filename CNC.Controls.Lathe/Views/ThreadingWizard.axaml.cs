using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Lathe;

public partial class ThreadingWizard : UserControl, ILatheWizardTab
{
    private bool _initOk;
    private readonly ThreadLogic _logic = new();
    private readonly ThreadModel _model;

    public ThreadingWizard()
    {
        InitializeComponent();
        DataContext = _model = _logic.Model;
        OutputActions.WizardModel = _model;
        OutputActions.CalculateAction = RunCalculate;
    }

    public LatheWizardType LatheWizardType => LatheWizardType.Threading;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThreadTypeCombo.ItemsSource = _model.Thread.Types.ToList();
        ThreadTypeCombo.SelectionChanged += (_, _) =>
        {
            if (ThreadTypeCombo.SelectedItem is KeyValuePair<Thread.Type, string> kv)
                _model.Thread.Type = kv.Key;
        };
        SyncThreadTypeCombo();
        _model.Thread.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThreadProperties.Type))
                SyncThreadTypeCombo();
        };
    }

    private void SyncThreadTypeCombo()
    {
        foreach (var item in _model.Thread.Types)
        {
            if (item.Key == _model.Thread.Type)
            {
                ThreadTypeCombo.SelectedItem = item;
                break;
            }
        }
    }

    public void Activate(bool activate)
    {
        if (!activate)
            return;

        if (!_initOk)
        {
            _initOk = true;
            _model.Profiles = _model.wz.Load();
            _model.config.Update();
            LatheConverters.IsMetric = _model.IsMetric = GrblParserState.IsMetric;
        }
        else
            _model.gCode.Clear();
    }

    void RunCalculate()
    {
        if (!_model.Thread.CompoundAngles.Contains(_model.Thread.CompoundAngle))
            _model.Thread.CompoundAngles.Add(_model.Thread.CompoundAngle);

        if (string.IsNullOrEmpty(_model.Thread.DepthDegression) && DepthDegressionCombo.SelectedItem is string depth)
            _model.Thread.DepthDegression = depth;

        _logic.Calculate();
    }
}
