using CNC.Core;
using CNC.App;
using CNC.App.Workspace;
using CNC.Platform.Abstractions;
using CNC.Platform.Linux;
using CNC.Platform.Windows;
using ioSender.Workspace;

namespace CNC.Platform.Tests;

public class PathServiceTests
{
    static readonly object EnvironmentLock = new();

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
    public void LinuxPathService_uses_xdg_config_home_for_config()
    {
        lock (EnvironmentLock)
        {
            var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configHome = Path.Combine(Path.GetTempPath(), "iosender-xdg-" + Guid.NewGuid().ToString("N"));

            try
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);

                var paths = new LinuxPathService();
                var result = paths.NormalizeConfigPath("/usr/lib/iosender");

                Assert.Equal(EnsureTrailingSeparator(Path.Combine(configHome, "ioSender")), result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            }
        }
    }

    [Fact]
    public void LinuxPathService_falls_back_to_home_config_directory()
    {
        lock (EnvironmentLock)
        {
            var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

            try
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);

                var paths = new LinuxPathService();
                var result = paths.NormalizeConfigPath("/usr/lib/iosender");
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                Assert.Equal(EnsureTrailingSeparator(Path.Combine(home, ".config", "ioSender")), result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            }
        }
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

    [Fact]
    public void AppConfigService_saves_config_to_linux_user_config_directory()
    {
        lock (EnvironmentLock)
        {
            var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-linux-config-test-" + Guid.NewGuid().ToString("N"));
            var installDir = Path.Combine(tempRoot, "install");
            var configHome = Path.Combine(tempRoot, "xdg");
            var expectedConfigDir = Path.Combine(configHome, "ioSender");

            try
            {
                Directory.CreateDirectory(installDir);
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);

                var appConfig = new AppConfigService(new LinuxPathService());
                appConfig.InitializePaths(installDir);
                appConfig.Base.Theme = "Light";

                Assert.True(appConfig.Save());
                Assert.True(File.Exists(Path.Combine(expectedConfigDir, "App.config")));
                Assert.Equal(Path.Combine(expectedConfigDir, "App.config"), appConfig.ConfigFilePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void WorkspaceLayoutFileService_saves_layouts_under_config_path()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-layout-test-" + Guid.NewGuid().ToString("N"));
        var originalConfigPath = Resources.ConfigPath;
        var originalPath = Resources.Path;

        try
        {
            Resources.Path = Resources.ConfigPath = EnsureTrailingSeparator(tempRoot);

            WorkspaceLayoutFileService.Save(
                "Shop",
                new WorkspaceLeaf { Editor = WorkspaceEditorId.Program });

            var expectedPath = Path.Combine(tempRoot, "layouts", "Shop.xml");
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            Resources.ConfigPath = originalConfigPath;
            Resources.Path = originalPath;
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

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
