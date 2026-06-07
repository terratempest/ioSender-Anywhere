using CNC.App;
using CNC.Core;
using ioSender.Services;

namespace CNC.Platform.Tests;

public sealed class ConsoleLogSettingsTests
{
    [Fact]
    public void Console_log_sync_copies_filter_ok_config_to_grbl()
    {
        var config = new BaseConfig { FilterOkResponse = true };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);

        Assert.True(grbl.ResponseLogFilterOk);
    }

    [Fact]
    public void Console_log_sync_copies_autoscroll_config_to_grbl()
    {
        var config = new BaseConfig { ConsoleAutoscroll = false };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);

        Assert.False(grbl.ResponseLogAutoscroll);
    }

    [Fact]
    public void Console_log_sync_updates_config_when_grbl_filter_ok_changes()
    {
        var config = new BaseConfig { FilterOkResponse = false };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);
        grbl.ResponseLogFilterOk = true;

        Assert.True(config.FilterOkResponse);
    }

    [Fact]
    public void Console_log_sync_updates_config_when_grbl_autoscroll_changes()
    {
        var config = new BaseConfig { ConsoleAutoscroll = true };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);
        grbl.ResponseLogAutoscroll = false;

        Assert.False(config.ConsoleAutoscroll);
    }

    [Fact]
    public void Console_log_sync_updates_grbl_when_config_filter_ok_changes()
    {
        var config = new BaseConfig { FilterOkResponse = false };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);
        config.FilterOkResponse = true;

        Assert.True(grbl.ResponseLogFilterOk);
    }

    [Fact]
    public void Console_log_sync_updates_grbl_when_config_autoscroll_changes()
    {
        var config = new BaseConfig { ConsoleAutoscroll = true };
        var grbl = new GrblViewModel();

        using var sync = ConsoleLogConfigSync.Attach(config, grbl);
        config.ConsoleAutoscroll = false;

        Assert.False(grbl.ResponseLogAutoscroll);
    }

    [Fact]
    public void ResponseLogText_updates_when_entries_are_appended()
    {
        var grbl = new GrblViewModel();

        grbl.ResponseLog.Add("first");
        grbl.ResponseLog.Add("second");

        Assert.Equal($"first{Environment.NewLine}second", grbl.ResponseLogText);
    }

    [Fact]
    public void ResponseLogText_clears_when_response_log_is_cleared()
    {
        var grbl = new GrblViewModel();
        grbl.ResponseLog.Add("first");

        grbl.ResponseLog.Clear();

        Assert.Equal(string.Empty, grbl.ResponseLogText);
    }

    [Fact]
    public void ResponseLogText_updates_when_entries_are_removed()
    {
        var grbl = new GrblViewModel();
        grbl.ResponseLog.Add("first");
        grbl.ResponseLog.Add("second");

        grbl.ResponseLog.RemoveAt(0);

        Assert.Equal("second", grbl.ResponseLogText);
    }

    [Fact]
    public void DataReceived_filters_ok_response_when_filter_ok_is_enabled()
    {
        var grbl = new GrblViewModel
        {
            ResponseLogVerbose = true,
            ResponseLogFilterOk = true,
        };

        grbl.DataReceived("ok");

        Assert.Empty(grbl.ResponseLog);
    }

    [Fact]
    public void DataReceived_logs_ok_response_when_filter_ok_is_disabled()
    {
        var grbl = new GrblViewModel
        {
            ResponseLogVerbose = true,
            ResponseLogFilterOk = false,
        };

        grbl.DataReceived("ok");

        Assert.Contains("ok", grbl.ResponseLog);
    }
}
