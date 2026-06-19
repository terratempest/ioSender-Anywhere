using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FeedControlLarge : UserControl
{
    readonly FeedPanelViewModel _viewModel;
    bool _useCoarseFeedStep;
    GrblViewModel? _subscribedModel;

    public FeedControlLarge() : this(null)
    {
    }

    public FeedControlLarge(MachineCommandService? commands)
    {
        _viewModel = new FeedPanelViewModel(commands);
        InitializeComponent();

        DataContextChanged += FeedControlLarge_DataContextChanged;
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
        TxtFeedRate.Text = "1250";
        TxtFeedOverride.Text = "100%";
        UpdateFeedStepText();
        SetRapidSelection(100);
    }

    void FeedControlLarge_DataContextChanged(object? sender, EventArgs e)
    {
        PropertyChangedSubscription.Swap(
            ref _subscribedModel,
            DataContext as GrblViewModel,
            OnDataContextPropertyChanged);
        _viewModel.Model = _subscribedModel;

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
        TxtFeedRate.Text = $"{_viewModel.FeedRateText} {_viewModel.Model.FeedrateUnit}";
        TxtFeedOverride.Text = $"{_viewModel.Model.FeedOverride:0}%";
        SetRapidSelection((int)_viewModel.Model.RapidsOverride);
    }

    bool UseCoarseFeedStep => _useCoarseFeedStep;

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
        _useCoarseFeedStep = !_useCoarseFeedStep;
        UpdateFeedStepText();
    }

    void RapidOverride_Click(object? sender, RoutedEventArgs e)
    {
        var value = sender switch
        {
            ToggleButton button when button == RbRapid25 => 25,
            ToggleButton button when button == RbRapid50 => 50,
            ToggleButton button when button == RbRapid100 => 100,
            _ => 0
        };

        if (value == 0)
            return;

        SetRapidSelection(value);
        _viewModel.SetRapidsOverride(value);
    }

    void BtnRapidOvrReset_Click(object? sender, RoutedEventArgs e)
    {
        SetRapidSelection(100);
        _viewModel.RapidsOverrideReset();
    }

    void SetRapidSelection(int value)
    {
        RbRapid25.IsChecked = value <= 25;
        RbRapid50.IsChecked = value > 25 && value <= 50;
        RbRapid100.IsChecked = value > 50;
    }

    void UpdateFeedStepText() => BtnFeedStep.Content = _useCoarseFeedStep ? "X10" : "X1";
}
