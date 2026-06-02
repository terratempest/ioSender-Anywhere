using CNC.App;
using CNC.Controls.Avalonia.Config;
using CNC.Controls.Avalonia.ViewModels;

namespace CNC.Platform.Tests;

public sealed class JogViewModelTests
{
    [Fact]
    public void SetMetric_applies_metric_app_config_values()
    {
        var config = new BaseConfig
        {
            JogUiMetric = new JogUIConfig(
                [11, 22, 33, 44],
                [0.11d, 0.22d, 0.33d, 0.44d])
        };
        var model = new JogViewModel();

        model.SetMetric(true, config);
        model.StepSize = JogViewModel.JogStep.Step3;
        model.Feed = JogViewModel.JogFeed.Feed2;

        Assert.Equal(0.11d, model.Distance0);
        Assert.Equal(0.22d, model.Distance1);
        Assert.Equal(0.33d, model.Distance2);
        Assert.Equal(0.44d, model.Distance3);
        Assert.Equal(11, model.Feedrate0);
        Assert.Equal(22, model.Feedrate1);
        Assert.Equal(33, model.Feedrate2);
        Assert.Equal(44, model.Feedrate3);
        Assert.Equal(0.44d, model.Distance);
        Assert.Equal(33d, model.FeedRate);
    }

    [Fact]
    public void SetMetric_applies_imperial_app_config_values()
    {
        var config = new BaseConfig
        {
            JogUiImperial = new JogUIConfig(
                [7, 8, 9, 10],
                [0.007d, 0.008d, 0.009d, 0.010d])
        };
        var model = new JogViewModel();

        model.SetMetric(false, config);
        model.StepSize = JogViewModel.JogStep.Step0;
        model.Feed = JogViewModel.JogFeed.Feed3;

        Assert.Equal(0.007d, model.Distance0);
        Assert.Equal(0.008d, model.Distance1);
        Assert.Equal(0.009d, model.Distance2);
        Assert.Equal(0.010d, model.Distance3);
        Assert.Equal(7, model.Feedrate0);
        Assert.Equal(8, model.Feedrate1);
        Assert.Equal(9, model.Feedrate2);
        Assert.Equal(10, model.Feedrate3);
        Assert.Equal(0.007d, model.Distance);
        Assert.Equal(10d, model.FeedRate);
    }

    [Fact]
    public void SetMetric_without_app_config_uses_defaults()
    {
        var model = new JogViewModel();

        model.SetMetric(true);

        Assert.Equal(JogDefaults.MetricDistances[0], model.Distance0);
        Assert.Equal(JogDefaults.MetricDistances[1], model.Distance1);
        Assert.Equal(JogDefaults.MetricDistances[2], model.Distance2);
        Assert.Equal(JogDefaults.MetricDistances[3], model.Distance3);
        Assert.Equal(JogDefaults.MetricFeedrates[0], model.Feedrate0);
        Assert.Equal(JogDefaults.MetricFeedrates[1], model.Feedrate1);
        Assert.Equal(JogDefaults.MetricFeedrates[2], model.Feedrate2);
        Assert.Equal(JogDefaults.MetricFeedrates[3], model.Feedrate3);
    }
}
