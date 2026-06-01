using CNC.Core;
using CNC.App;
using CNC.Platform.Abstractions;
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
    public void WindowsPathService_uses_user_local_app_data_for_config()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return;

        var paths = new WindowsPathService();
        var result = paths.NormalizeConfigPath(@"C:\Program Files\ioSender");

        Assert.StartsWith(Path.Combine(localAppData, "ioSender"), result, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void AppConfigService_migrates_legacy_executable_folder_config()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-config-test-" + Guid.NewGuid().ToString("N"));
        var installDir = Path.Combine(tempRoot, "install");
        var configDir = Path.Combine(tempRoot, "config");
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(configDir);

        try
        {
            var legacyPath = Path.Combine(installDir, "App.config");
            var legacy = new AppConfigService();
            legacy.Base.Theme = "Light";
            Assert.True(legacy.Save(legacyPath));

            var appConfig = new AppConfigService(new FixedConfigPathService(configDir));
            appConfig.InitializePaths(installDir);

            Assert.True(appConfig.EnsureLoaded());
            Assert.Equal("Light", appConfig.Base.Theme);
            Assert.True(File.Exists(Path.Combine(configDir, "App.config")));
            Assert.Equal(Path.Combine(configDir, "App.config"), appConfig.ConfigFilePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    sealed class FixedConfigPathService : IPathService
    {
        readonly string _configPath;

        public FixedConfigPathService(string configPath) => _configPath = configPath;

        public string Combine(params string[] segments) => Path.Combine(segments);

        public string NormalizeConfigPath(string path)
        {
            var full = Path.GetFullPath(_configPath);
            return full.EndsWith(Path.DirectorySeparatorChar)
                ? full
                : full + Path.DirectorySeparatorChar;
        }

        public bool IsPhysicalFilePath(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);
    }
}
