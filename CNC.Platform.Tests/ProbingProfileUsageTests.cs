using CNC.Controls.Probing;
using CNC.Core;

namespace CNC.Platform.Tests;

public class ProbingProfileUsageTests
{
    [Fact]
    public void StorePersistsProfilesPerProbingTab()
    {
        WithTempConfigPath(() =>
        {
            var store = new ProbingProfileUsageStore();
            store.Set(ProbingType.ToolLength, Profile("Tool"));
            store.Set(ProbingType.EdgeFinderExternal, Profile("External"));
            store.Set(ProbingType.EdgeFinderInternal, Profile("Internal"));
            store.Set(ProbingType.Rotation, Profile("Rotation"));
            store.Set(ProbingType.CenterFinder, Profile("Center"));
            store.Set(ProbingType.HeightMap, Profile("Height"));
            store.Save();

            var restored = new ProbingProfileUsageStore();
            restored.Load();

            Assert.Equal("Tool", restored.Get(ProbingType.ToolLength));
            Assert.Equal("External", restored.Get(ProbingType.EdgeFinderExternal));
            Assert.Equal("Internal", restored.Get(ProbingType.EdgeFinderInternal));
            Assert.Equal("Rotation", restored.Get(ProbingType.Rotation));
            Assert.Equal("Center", restored.Get(ProbingType.CenterFinder));
            Assert.Equal("Height", restored.Get(ProbingType.HeightMap));
        });
    }

    [Fact]
    public void ViewModelRestoresProfilesIndependentlyPerTab()
    {
        WithTempConfigPath(() =>
        {
            var model = new ProbingViewModel();
            var toolId = model.ProfileStore.Add("Tool profile", model);
            var edgeId = model.ProfileStore.Add("Edge profile", model);

            model.Profile = model.Profiles.First(p => p.Id == toolId);
            model.RememberProfileForTab(ProbingType.ToolLength);

            model.Profile = model.Profiles.First(p => p.Id == edgeId);
            model.RememberProfileForTab(ProbingType.EdgeFinderExternal);

            model.RestoreProfileForTab(ProbingType.ToolLength);
            Assert.Equal("Tool profile", model.Profile?.Name);

            model.RestoreProfileForTab(ProbingType.EdgeFinderExternal);
            Assert.Equal("Edge profile", model.Profile?.Name);
        });
    }

    [Fact]
    public void ViewModelFallsBackToFirstProfileWhenRememberedProfileIsMissing()
    {
        WithTempConfigPath(() =>
        {
            var store = new ProbingProfileUsageStore();
            store.Set(ProbingType.ToolLength, Profile("Deleted profile"));
            store.Save();

            var model = new ProbingViewModel();
            model.ProfileUsageStore.Load();
            model.ProfileStore.Add("First profile", model);
            model.ProfileStore.Add("Second profile", model);

            model.RestoreProfileForTab(ProbingType.ToolLength);

            Assert.Equal("First profile", model.Profile?.Name);
        });
    }

    static ProbingProfile Profile(string name) => new() { Name = name };

    static void WithTempConfigPath(System.Action action)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-probing-profile-usage-test-" + Guid.NewGuid().ToString("N"));
        var originalPath = Resources.Path;
        var originalConfigPath = Resources.ConfigPath;

        try
        {
            Directory.CreateDirectory(tempRoot);
            Resources.Path = Resources.ConfigPath = EnsureTrailingSeparator(tempRoot);
            action();
        }
        finally
        {
            Resources.Path = originalPath;
            Resources.ConfigPath = originalConfigPath;
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
