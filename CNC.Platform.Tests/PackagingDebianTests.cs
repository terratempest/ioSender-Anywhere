namespace CNC.Platform.Tests;

public sealed class PackagingDebianTests
{
    [Fact]
    public void Debian_package_template_contains_iosender_udev_rule()
    {
        var root = FindRepositoryRoot();
        var rulesPath = Path.Combine(
            root,
            "packaging",
            "debian",
            "usr",
            "lib",
            "udev",
            "rules.d",
            "70-iosender-serial.rules");

        var rules = File.ReadAllText(rulesPath);

        Assert.Contains("TAG+=\"uaccess\"", rules);
        Assert.Contains("MODE=\"0660\"", rules);
        Assert.Contains("ttyUSB[0-9]*", rules);
        Assert.Contains("ttyACM[0-9]*", rules);
        Assert.Contains("ttyAMA[0-9]*", rules);
        Assert.Contains("ttyTHS[0-9]*", rules);
        Assert.Contains("ttymxc[0-9]*", rules);
        Assert.Contains("ttyGS[0-9]*", rules);
    }

    [Fact]
    public void Debian_postinst_reloads_udev_without_dialout_prompt()
    {
        var root = FindRepositoryRoot();
        var postinst = File.ReadAllText(Path.Combine(root, "packaging", "debian", "DEBIAN", "postinst"));

        Assert.Contains("udevadm control --reload-rules", postinst);
        Assert.Contains("udevadm trigger --subsystem-match=tty", postinst);
        Assert.DoesNotContain("usermod", postinst);
        Assert.DoesNotContain("dialout", postinst);
    }

    [Fact]
    public void Debian_package_script_stages_iosender_udev_rule()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "scripts", "package-deb.sh"));

        Assert.Contains("usr/lib/udev/rules.d", packageScript);
        Assert.Contains("70-iosender-serial.rules", packageScript);
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
