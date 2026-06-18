namespace CNC.Platform.Tests;

public sealed class PackagingMatrixTests
{
    [Fact]
    public void Windows_publish_script_accepts_x64_and_arm64_rids()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "publish-windows.ps1"));

        Assert.Contains("[ValidateSet('win-x64', 'win-arm64')]", script);
        Assert.Contains("-r $RuntimeIdentifier", script);
        Assert.Contains("artifacts\\publish\\$RuntimeIdentifier", script);
    }

    [Fact]
    public void Windows_installer_script_sets_architecture_by_rid()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "package-windows-installer.ps1"));
        var installerTemplate = File.ReadAllText(Path.Combine(root, "packaging", "windows", "iosender.iss"));

        Assert.Contains("win-arm64", script);
        Assert.Contains("\"arm64\"", script);
        Assert.Contains("\"x64compatible\"", script);
        Assert.Contains("/DArchitecturesAllowed=$installerArchitecture", script);
        Assert.Contains("ArchitecturesAllowed={#ArchitecturesAllowed}", installerTemplate);
    }

    [Fact]
    public void Debian_script_maps_linux_rids_to_debian_architectures()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "package-deb.sh"));
        var control = File.ReadAllText(Path.Combine(root, "packaging", "debian", "DEBIAN", "control.in"));

        Assert.Contains("linux-x64)", script);
        Assert.Contains("DEB_ARCH=\"amd64\"", script);
        Assert.Contains("linux-arm64)", script);
        Assert.Contains("DEB_ARCH=\"arm64\"", script);
        Assert.Contains("iosender_${VERSION}_${DEB_ARCH}.deb", script);
        Assert.Contains("Architecture: @ARCHITECTURE@", control);
    }

    [Fact]
    public void Rpm_and_appimage_scripts_map_linux_rids_to_package_architectures()
    {
        var root = FindRepositoryRoot();
        var rpm = File.ReadAllText(Path.Combine(root, "scripts", "package-rpm.sh"));
        var appImage = File.ReadAllText(Path.Combine(root, "scripts", "package-appimage.sh"));
        var dependencyCheck = File.ReadAllText(Path.Combine(root, "scripts", "check-linux-deps.sh"));

        Assert.Contains("RPM_ARCH=\"x86_64\"", rpm);
        Assert.Contains("RPM_ARCH=\"aarch64\"", rpm);
        Assert.Contains("ioSender-$VERSION-$RID.rpm", rpm);
        Assert.Contains("--target \"$RPM_ARCH-linux\"", rpm);
        Assert.Contains("buildarch_compat: $HOST_RPM_ARCH: $RPM_ARCH noarch", rpm);
        Assert.Contains("__brp_strip %{nil}", rpm);
        Assert.Contains("APPIMAGE_ARCH=\"x86_64\"", appImage);
        Assert.Contains("APPIMAGE_ARCH=\"aarch64\"", appImage);
        Assert.Contains("ioSender-$VERSION-$RID.AppImage", appImage);
        Assert.Contains("sed -i 's/\\r$//'", appImage);
        Assert.Contains("\"$APPDIR/ioSender.desktop\"", appImage);
        Assert.Contains("artifacts/publish/$RID", dependencyCheck);
    }

    [Fact]
    public void OpenCv_runtime_packages_cover_release_rids()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "CNC.Platform.Camera.OpenCv", "CNC.Platform.Camera.OpenCv.csproj"));

        Assert.Contains("OpenCvSharp4.runtime.win", project);
        Assert.Contains("OpenCvSharp4.runtime.win-arm64", project);
        Assert.Contains("OpenCvSharp4.official.runtime.linux-x64", project);
        Assert.Contains("OpenCvSharp4.runtime.linux-arm64", project);
    }

    [Fact]
    public void Build_orchestrator_exposes_matrix_targets_and_rid_filter()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "build-all.ps1"));

        Assert.Contains("'Windows'", script);
        Assert.Contains("'LinuxRpm'", script);
        Assert.Contains("'LinuxAppImage'", script);
        Assert.Contains("[Alias('Rid')]", script);
        Assert.Contains("'win-arm64'", script);
        Assert.Contains("'linux-arm64'", script);
    }

    [Fact]
    public void Build_orchestrator_defaults_to_full_linux_rid_matrix()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "build-all.ps1"));

        Assert.Contains("$LinuxRids = @('linux-x64', 'linux-arm64')", script);
        Assert.Contains("return $LinuxRids", script);
        Assert.DoesNotContain("DefaultLinuxRid", script);
        Assert.DoesNotContain("packages require matching WSL/native host", script);
    }

    [Fact]
    public void Build_orchestrator_keeps_explicit_linux_rid_filter()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "build-all.ps1"));

        Assert.Contains("if ($RuntimeIdentifier.StartsWith('linux')) { return @($RuntimeIdentifier) }", script);
    }

    [Fact]
    public void Build_orchestrator_preflights_optional_linux_packaging_tools()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "build-all.ps1"));

        Assert.Contains("Test-WslCommandAvailable", script);
        Assert.Contains("CommandName 'rpmbuild'", script);
        Assert.Contains("sudo apt install -y rpm", script);
        Assert.Contains("CommandName 'appimagetool'", script);
        Assert.Contains("appimagetool is required", script);
    }

    [Fact]
    public void Build_console_uses_linear_output_without_cursor_redraw()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "BuildConsole.ps1"));

        Assert.Contains("function Start-PhaseBoardDisplay", script);
        Assert.Contains("function Render-PhaseBoard", script);
        Assert.Contains("function Finish-PhaseBoard", script);
        Assert.Contains("return", script);
        Assert.Contains("{0,-24} {1,-8}", script);
        Assert.Contains("[{0}] {1}: {2}", script);
        Assert.Contains("Write-PlainLine", script);
        Assert.DoesNotContain("Write-Host ($text.PadRight($width - 1)) -NoNewline", script);
        Assert.DoesNotContain("-ForegroundColor", script);
        Assert.DoesNotContain("{0,-12} {1,-12} {2}", script);
    }

    [Fact]
    public void Linux_scripts_allow_foreign_rid_packaging_without_ldd()
    {
        var root = FindRepositoryRoot();
        var buildLinux = File.ReadAllText(Path.Combine(root, "scripts", "build-linux.sh"));
        var dependencyCheck = File.ReadAllText(Path.Combine(root, "scripts", "check-linux-deps.sh"));

        Assert.DoesNotContain("packages require a matching native host", buildLinux);
        Assert.Contains("Skipping ldd for foreign RID $RID on $HOST_RID host.", dependencyCheck);
        Assert.Contains("Published binary exists: $BINARY", dependencyCheck);
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ioSender.net.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
