using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

using CNC.Core;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FeedControlTouch : UserControl
{
    readonly FeedPanelViewModel _viewModel;
    GrblViewModel? _subscribedModel;

    public FeedControlTouch() : this(null)
    {
    }

    public FeedControlTouch(MachineCommandService? commands)
    {
        _viewModel = new FeedPanelViewModel(commands);
        InitializeComponent();
        DataContextChanged += FeedControlTouch_DataContextChanged;
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

    void FeedControlTouch_DataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedModel is INotifyPropertyChanged oldPc)
            oldPc.PropertyChanged -= OnDataContextPropertyChanged;

        _subscribedModel = DataContext as GrblViewModel;
        _viewModel.Model = _subscribedModel;

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
        if (_viewModel.Model == null)
            return;

        _viewModel.UpdateFromModel();
        cvFeedRate.Text = _viewModel.FeedRateText;
        LblFeedOverrideSummary.Text = _viewModel.FeedOverrideSummary;
        LblFeedOverride.Text = _viewModel.FeedOverrideText;
        LblRapidsOverride.Text = _viewModel.RapidsOverrideText;
    }

    bool UseCoarseFeedStep => RbFeedStepCoarse.IsChecked == true;

    void BtnFeedOvrPlus_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.UseCoarseFeedStep = UseCoarseFeedStep;
        _viewModel.FeedOverridePlus();
    }

    void BtnFeedOvrMinus_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.UseCoarseFeedStep = UseCoarseFeedStep;
        _viewModel.FeedOverrideMinus();
    }

    void BtnFeedOvrReset_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.FeedOverrideReset();

    void FeedOvrStep_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton selected)
            return;

        RbFeedStepFine.IsChecked = selected == RbFeedStepFine;
        RbFeedStepCoarse.IsChecked = selected == RbFeedStepCoarse;
    }

    void BtnRapidOvrPlus_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.RapidsOverridePlus();

    void BtnRapidOvrMinus_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.RapidsOverrideMinus();

    void BtnRapidOvrReset_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.RapidsOverrideReset();
}
