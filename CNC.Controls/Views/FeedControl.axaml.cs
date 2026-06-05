using Avalonia;
using Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FeedControl : UserControl
{
    readonly FeedPanelViewModel _viewModel;

    public FeedControl() : this(null)
    {
    }

    public FeedControl(MachineCommandService? commands)
    {
        _viewModel = new FeedPanelViewModel(commands);
        InitializeComponent();

        DataContextChanged += (_, _) => _viewModel.Model = DataContext as GrblViewModel;

        feedOverrideControl.ResetCommand = GrblConstants.CMD_FEED_OVR_RESET;
        feedOverrideControl.FineMinusCommand = GrblConstants.CMD_FEED_OVR_FINE_MINUS;
        feedOverrideControl.FinePlusCommand = GrblConstants.CMD_FEED_OVR_FINE_PLUS;
        feedOverrideControl.CoarseMinusCommand = GrblConstants.CMD_FEED_OVR_COARSE_MINUS;
        feedOverrideControl.CoarsePlusCommand = GrblConstants.CMD_FEED_OVR_COARSE_PLUS;
        feedOverrideControl.CommandGenerated += OverrideControl_CommandGenerated;

        rapidsOverrideControl.ResetCommand = GrblConstants.CMD_RAPID_OVR_RESET;
        rapidsOverrideControl.FineMinusCommand = GrblConstants.CMD_RAPID_OVR_MEDIUM;
        rapidsOverrideControl.CoarseMinusCommand = GrblConstants.CMD_RAPID_OVR_LOW;
        rapidsOverrideControl.Ticks = [25, 50, 100];
        rapidsOverrideControl.Minimum = 25;
        rapidsOverrideControl.Maximum = 100;
        SetRapidsOverrideDefault();
        rapidsOverrideControl.CommandGenerated += OverrideControl_CommandGenerated;

        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
                return;

            ApplyLocalization();
            SetRapidsOverrideDefault();
        };
    }

    void ApplyLocalization()
    {
        Localize.Apply(lblFeedOverride);
        Localize.Apply(lblRapids);
    }

    void OverrideControl_CommandGenerated(byte[] commands, int len) =>
        _viewModel.SendOverrideCommands(commands, len);

    void SetRapidsOverrideDefault()
    {
        rapidsOverrideControl.SliderValue = 100d;
    }
}
