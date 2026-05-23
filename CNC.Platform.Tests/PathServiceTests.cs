using CNC.Core;
using CNC.Platform.Linux;
using CNC.Platform.Windows;

namespace CNC.Platform.Tests;

public class PathServiceTests
{
    [Fact]
    public void NormalizeConfigPath_ensures_trailing_separator()
    {
        var paths = new WindowsPathService();
        var result = paths.NormalizeConfigPath(@"C:\ioSender\config");
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
    }

    [Fact]
    public void IsPhysicalFilePath_detects_unc_and_drive()
    {
        var paths = new WindowsPathService();
        Assert.True(paths.IsPhysicalFilePath(@"\\server\share\file.nc"));
        Assert.True(paths.IsPhysicalFilePath(@"C:\jobs\test.nc"));
        Assert.False(paths.IsPhysicalFilePath("macros:foo"));
    }

    [Fact]
    public void LinuxPathService_detects_absolute_and_home_paths()
    {
        var paths = new LinuxPathService();
        Assert.True(paths.IsPhysicalFilePath("/home/user/jobs/test.nc"));
        Assert.True(paths.IsPhysicalFilePath("~/jobs/test.nc"));
        Assert.False(paths.IsPhysicalFilePath("macros:foo"));
        Assert.False(paths.IsPhysicalFilePath("http://example.test/file.nc"));
    }

    [Fact]
    public void GrblViewModel_uses_injected_path_service_for_physical_file_detection()
    {
        var vm = new GrblViewModel { PathService = new LinuxPathService() };
        vm.FileName = "/home/user/jobs/test.nc";
        Assert.True(vm.IsPhysicalFileLoaded);
    }
}
