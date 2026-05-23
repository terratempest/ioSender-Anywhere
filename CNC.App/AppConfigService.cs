using System.Xml.Serialization;
using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.App;

public sealed class AppConfigService
{
    private readonly IPathService? _pathService;
    private string? _configFilePath;

    public AppConfigService(IPathService? pathService = null) => _pathService = pathService;

    public BaseConfig Base { get; private set; } = new();

    public string? FileName { get; private set; }

    public string ConfigFilePath =>
        _configFilePath ?? Resources.IniFile;

    public void InitializePaths(string baseDirectory)
    {
        var path = _pathService?.NormalizeConfigPath(baseDirectory) ?? baseDirectory;
        if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;

        Resources.Path = Resources.ConfigPath = path;
    }

    public bool Load(string? filename = null)
    {
        var path = filename ?? ConfigFilePath;
        var serializer = new XmlSerializer(typeof(BaseConfig));

        try
        {
            using var reader = new StreamReader(path);
            Base = (BaseConfig)serializer.Deserialize(reader)!;
            _configFilePath = path;

            var sessionMacros = Base.Macros.Where(m => m.IsSession).ToList();
            foreach (var macro in sessionMacros)
                Base.Macros.Remove(macro);

            return true;
        }
        catch
        {
            Base = new BaseConfig();
            return false;
        }
    }

    public bool Save(string? filename = null)
    {
        var path = filename ?? _configFilePath ?? ConfigFilePath;
        var serializer = new XmlSerializer(typeof(BaseConfig));

        try
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            serializer.Serialize(stream, Base);
            _configFilePath = path;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Shutdown()
    {
        if (Base.Camera.IsDirty)
            Save();
    }

    public bool EnsureLoaded()
    {
        if (Load())
        {
            var migrated = MigrateLegacyTheme(Base);
            if (MigrateWorkspaceLayout(Base))
                migrated = true;
            if (migrated)
                Save();
            return true;
        }

        Base = new BaseConfig();
        return Save();
    }

    static bool MigrateLegacyTheme(BaseConfig config)
    {
        var theme = config.Theme;
        if (string.IsNullOrWhiteSpace(theme)
            || theme.Equals("default", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            config.Theme = "Standard";
            return true;
        }

        return false;
    }

    /// <summary>Ensure workspace root exists; map legacy LayoutMode when root is missing.</summary>
    public static bool MigrateWorkspaceLayout(BaseConfig config)
    {
        if (config.WorkspaceRoot is not null)
            return false;

        config.WorkspacePreset = config.LayoutMode == UiLayoutMode.Expanded ? "Expanded" : "Compact";
        return false;
    }
}
