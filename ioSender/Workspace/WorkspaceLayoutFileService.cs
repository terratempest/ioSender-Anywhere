using System.Xml.Serialization;
using CNC.App;
using CNC.App.Workspace;
using CNC.Core;

namespace ioSender.Workspace;

public sealed record WorkspaceLayoutFile(string Name, string Path);

public static class WorkspaceLayoutFileService
{
    const string LayoutExtension = ".xml";

    static readonly XmlSerializer Serializer = new(typeof(WorkspaceSavedLayout));

    public static string LayoutsDirectory => Path.Combine(Resources.ConfigPath, "layouts");

    public static IReadOnlyList<WorkspaceLayoutFile> LoadLayouts()
    {
        if (!Directory.Exists(LayoutsDirectory))
            return Array.Empty<WorkspaceLayoutFile>();

        var layouts = new List<WorkspaceLayoutFile>();
        foreach (var file in Directory.EnumerateFiles(LayoutsDirectory, "*" + LayoutExtension))
        {
            if (TryLoad(file, out var layout) && WorkspaceLayoutDefaults.IsValid(layout.Root))
                layouts.Add(new WorkspaceLayoutFile(layout.Name, file));
        }

        return layouts
            .Where(l => !WorkspaceLayoutDefaults.IsBuiltIn(l.Name))
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static bool TryLoadByName(string name, out WorkspaceSavedLayout layout)
    {
        foreach (var file in LoadLayouts())
        {
            if (file.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && TryLoad(file.Path, out layout))
                return true;
        }

        layout = new WorkspaceSavedLayout();
        return false;
    }

    public static void Save(string name, WorkspaceNode root, QuickAccessSidebarConfig? quickAccessSidebar = null)
    {
        Directory.CreateDirectory(LayoutsDirectory);

        var existing = LoadLayouts()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? GetUniquePath(name);
        var layout = new WorkspaceSavedLayout
        {
            Name = name,
            Root = root.Clone(),
            QuickAccessSidebar = quickAccessSidebar?.Clone(),
        };

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Serializer.Serialize(stream, layout);
    }

    public static bool Delete(string name)
    {
        var layout = LoadLayouts()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (layout is null)
            return false;

        File.Delete(layout.Path);
        return true;
    }

    static bool TryLoad(string path, out WorkspaceSavedLayout layout)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            layout = (WorkspaceSavedLayout)Serializer.Deserialize(stream)!;
            layout.Name = layout.Name.Trim();
            if (!string.IsNullOrWhiteSpace(layout.Name))
                return true;
        }
        catch
        {
        }

        layout = new WorkspaceSavedLayout();
        return false;
    }

    static string GetUniquePath(string name)
    {
        var safeName = SanitizeFileName(name);
        var path = Path.Combine(LayoutsDirectory, safeName + LayoutExtension);
        if (!File.Exists(path))
            return path;

        for (var i = 2; ; i++)
        {
            path = Path.Combine(LayoutsDirectory, $"{safeName}-{i}{LayoutExtension}");
            if (!File.Exists(path))
                return path;
        }
    }

    static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = name
            .Trim()
            .Select(c => invalid.Contains(c) ? '-' : c)
            .ToArray();
        var safe = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "layout" : safe;
    }
}
