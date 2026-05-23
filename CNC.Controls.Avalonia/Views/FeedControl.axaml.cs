using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FeedControl : UserControl
{
    public FeedControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLocalization();
    }

    void ApplyLocalization()
    {
        Localize.Apply(LblFeedRate);
        Localize.Apply(LblRapids);
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
