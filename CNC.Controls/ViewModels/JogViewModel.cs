using CNC.Controls.Avalonia.Config;
using CNC.App;
using CNC.Core;

namespace CNC.Controls.Avalonia.ViewModels;

public class JogViewModel : ViewModelBase
{
    public static JogViewModel Shared { get; } = CreateShared();

    public enum JogStep
    {
        Step0 = 0,
        Step1,
        Step2,
        Step3,
        Continuous
    }

    public enum JogFeed
    {
        Feed0 = 0,
        Feed1,
        Feed2,
        Feed3
    }

    JogStep _jogStep = JogStep.Step1;
    JogFeed _jogFeed = JogFeed.Feed1;
    readonly double[] _distance = new double[5];
    readonly int[] _feedRate = new int[4];

    static JogViewModel CreateShared()
    {
        var model = new JogViewModel();
        model.SetMetric(true);
        return model;
    }

    public void SetMetric(bool on, BaseConfig? config = null)
    {
        var feeds = config is null
            ? (on ? JogDefaults.MetricFeedrates : JogDefaults.ImperialFeedrates)
            : (on ? config.JogUiMetric.Feedrate : config.JogUiImperial.Feedrate);
        var distances = config is null
            ? (on ? JogDefaults.MetricDistances : JogDefaults.ImperialDistances)
            : (on ? config.JogUiMetric.Distance : config.JogUiImperial.Distance);
        for (var i = 0; i < _feedRate.Length; i++)
        {
            _distance[i] = distances[i];
            _feedRate[i] = feeds[i];
            OnPropertyChanged("Feedrate" + i);
            OnPropertyChanged("Distance" + i);
        }
        _distance[(int)JogStep.Continuous] = -1d;
        OnPropertyChanged(nameof(Distance));
        OnPropertyChanged(nameof(FeedRate));
    }

    public JogStep StepSize
    {
        get => _jogStep;
        set
        {
            if (_jogStep == value)
                return;
            _jogStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Distance));
        }
    }

    public double Distance => _distance[(int)_jogStep];

    public JogFeed Feed
    {
        get => _jogFeed;
        set
        {
            if (_jogFeed == value)
                return;
            _jogFeed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FeedRate));
        }
    }

    public double FeedRate => _feedRate[(int)_jogFeed];

    public int Feedrate0 => _feedRate[0];
    public int Feedrate1 => _feedRate[1];
    public int Feedrate2 => _feedRate[2];
    public int Feedrate3 => _feedRate[3];

    public double Distance0 => _distance[0];
    public double Distance1 => _distance[1];
    public double Distance2 => _distance[2];
    public double Distance3 => _distance[3];

    public void StepInc()
    {
        if (StepSize != JogStep.Continuous)
            StepSize += 1;
    }

    public void CycleStepSkippingContinuous()
    {
        StepSize = StepSize is JogStep.Step3 or JogStep.Continuous
            ? JogStep.Step0
            : StepSize + 1;
    }

    public void StepDec()
    {
        if (StepSize != JogStep.Step0)
            StepSize -= 1;
    }

    public void FeedInc()
    {
        if (Feed != JogFeed.Feed3)
            Feed += 1;
    }

    public void CycleFeed()
    {
        Feed = Feed == JogFeed.Feed3 ? JogFeed.Feed0 : Feed + 1;
    }

    public void FeedDec()
    {
        if (Feed != JogFeed.Feed0)
            Feed -= 1;
    }
}
