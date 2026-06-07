using System.ComponentModel;
using CNC.App;
using CNC.Core;

namespace ioSender.Services;

public sealed class ConsoleLogConfigSync : IDisposable
{
    readonly BaseConfig _config;
    readonly GrblViewModel _grbl;

    public ConsoleLogConfigSync(BaseConfig config, GrblViewModel grbl)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _grbl = grbl ?? throw new ArgumentNullException(nameof(grbl));

        _grbl.ResponseLogFilterOk = _config.FilterOkResponse;
        _grbl.ResponseLogAutoscroll = _config.ConsoleAutoscroll;
        _config.PropertyChanged += OnConfigPropertyChanged;
        _grbl.PropertyChanged += OnGrblPropertyChanged;
    }

    public static ConsoleLogConfigSync Attach(BaseConfig config, GrblViewModel grbl) =>
        new(config, grbl);

    void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BaseConfig.FilterOkResponse):
                if (_grbl.ResponseLogFilterOk != _config.FilterOkResponse)
                    _grbl.ResponseLogFilterOk = _config.FilterOkResponse;
                break;

            case nameof(BaseConfig.ConsoleAutoscroll):
                if (_grbl.ResponseLogAutoscroll != _config.ConsoleAutoscroll)
                    _grbl.ResponseLogAutoscroll = _config.ConsoleAutoscroll;
                break;
        }
    }

    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.ResponseLogFilterOk):
                if (_config.FilterOkResponse != _grbl.ResponseLogFilterOk)
                    _config.FilterOkResponse = _grbl.ResponseLogFilterOk;
                break;

            case nameof(GrblViewModel.ResponseLogAutoscroll):
                if (_config.ConsoleAutoscroll != _grbl.ResponseLogAutoscroll)
                    _config.ConsoleAutoscroll = _grbl.ResponseLogAutoscroll;
                break;
        }
    }

    public void Dispose()
    {
        _config.PropertyChanged -= OnConfigPropertyChanged;
        _grbl.PropertyChanged -= OnGrblPropertyChanged;
    }
}
