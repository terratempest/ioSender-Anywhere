using System.ComponentModel;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FeedControl : UserControl
{
    GrblViewModel? _subscribedModel;

    public FeedControl()
    {
        InitializeComponent();
        DataContextChanged += FeedControl_DataContextChanged;
        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
            {
                ApplyDesignPreviewValues();
                return;
            }

            ApplyLocalization();
            UpdateFeedText();
        };

        if (Design.IsDesignMode)
            ApplyDesignPreviewValues();
    }

    void ApplyLocalization()
    {
        Localize.Apply(LblFeedRate);
        Localize.Apply(LblRapids);
    }

    void ApplyDesignPreviewValues()
    {
        cvFeedRate.Text = "1250";
        LblFeedOverrideSummary.Text = "mm/min % 100";
        LblFeedOverride.Text = "Feed % 100";
        LblRapidsOverride.Text = "%100";
    }

    void FeedControl_DataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedModel is INotifyPropertyChanged oldPc)
            oldPc.PropertyChanged -= OnDataContextPropertyChanged;

        _subscribedModel = DataContext as GrblViewModel;

        if (_subscribedModel is INotifyPropertyChanged newPc)
            newPc.PropertyChanged += OnDataContextPropertyChanged;

        UpdateFeedText();
    }

    void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.FeedRate)
            or nameof(GrblViewModel.FeedOverride)
            or nameof(GrblViewModel.RapidsOverride)
            or nameof(GrblViewModel.FeedrateUnit))
            UpdateFeedText();
    }

    void UpdateFeedText()
    {
        if (DataContext is not GrblViewModel model)
            return;

        cvFeedRate.Text = model.FeedRate.ToString("####0", CultureInfo.InvariantCulture);
        LblFeedOverrideSummary.Text = $"{model.FeedrateUnit} % {model.FeedOverride:0}";
        LblFeedOverride.Text = $"Feed % {model.FeedOverride:0}";
        LblRapidsOverride.Text = $"%{model.RapidsOverride:0}";
    }

    bool UseCoarseFeedStep => RbFeedStepCoarse.IsChecked == true;

    void BtnFeedOvrPlus_Click(object? sender, RoutedEventArgs e) =>
        SendOverride(UseCoarseFeedStep ? GrblConstants.CMD_FEED_OVR_COARSE_PLUS : GrblConstants.CMD_FEED_OVR_FINE_PLUS);

    void BtnFeedOvrMinus_Click(object? sender, RoutedEventArgs e) =>
        SendOverride(UseCoarseFeedStep ? GrblConstants.CMD_FEED_OVR_COARSE_MINUS : GrblConstants.CMD_FEED_OVR_FINE_MINUS);

    void BtnFeedOvrReset_Click(object? sender, RoutedEventArgs e) =>
        SendOverride(GrblConstants.CMD_FEED_OVR_RESET);

    void FeedOvrStep_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton selected)
            return;

        RbFeedStepFine.IsChecked = selected == RbFeedStepFine;
        RbFeedStepCoarse.IsChecked = selected == RbFeedStepCoarse;
    }

    void BtnRapidOvrPlus_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model)
            return;
        if (model.RapidsOverride <= 25)
            SendOverride(GrblConstants.CMD_RAPID_OVR_MEDIUM);
        else if (model.RapidsOverride <= 50)
            SendOverride(GrblConstants.CMD_RAPID_OVR_RESET);
    }

    void BtnRapidOvrMinus_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model)
            return;
        if (model.RapidsOverride > 50)
            SendOverride(GrblConstants.CMD_RAPID_OVR_MEDIUM);
        else if (model.RapidsOverride > 25)
            SendOverride(GrblConstants.CMD_RAPID_OVR_LOW);
    }

    void BtnRapidOvrReset_Click(object? sender, RoutedEventArgs e) =>
        SendOverride(GrblConstants.CMD_RAPID_OVR_RESET);

    static void SendOverride(byte command) => Comms.com?.WriteByte(command);
}
