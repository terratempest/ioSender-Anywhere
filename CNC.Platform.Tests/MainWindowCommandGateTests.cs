using ioSender;

namespace CNC.Platform.Tests;

public sealed class MainWindowCommandGateTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void CanMutateProgramState_BlocksOnlyMachineBusyOrLoading(
        bool isJobRunning,
        bool isToolChanging,
        bool isProgramLoading,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.CanMutateProgramState(isJobRunning, isToolChanging, isProgramLoading));
    }
}
