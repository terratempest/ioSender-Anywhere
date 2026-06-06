using System.Xml.Serialization;
using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.App;

public sealed class AppConfigService
{
    private readonly IPathService? _pathService;
    private string? _configFilePath;
    private string? _legacyConfigFilePath;

    public AppConfigService(IPathService? pathService = null) => _pathService = pathService;

    public BaseConfig Base { get; private set; } = new();

    public event EventHandler? Saved;

    public string? FileName { get; private set; }

    public string ConfigFilePath =>
        _configFilePath ?? Resources.IniFile;

    public void InitializePaths(string baseDirectory)
    {
        var path = _pathService?.NormalizeConfigPath(baseDirectory) ?? baseDirectory;
        if (!path.EndsWith(Path.DirectorySeparatorChar))
            path += Path.DirectorySeparatorChar;

        Resources.Path = Resources.ConfigPath = path;

        var legacyPath = Path.Combine(baseDirectory, Resources.IniName);
        if (!string.Equals(legacyPath, Resources.IniFile, StringComparison.OrdinalIgnoreCase))
            _legacyConfigFilePath = legacyPath;
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
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            serializer.Serialize(stream, Base);
            _configFilePath = path;
            Saved?.Invoke(this, EventArgs.Empty);
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
            if (MigrateQuickAccessSidebar(Base))
                migrated = true;
            if (MigrateGameController(Base))
                migrated = true;
            if (MigrateThemes(Base))
                migrated = true;
            if (migrated)
                Save();
            return true;
        }

        var targetConfigFilePath = Resources.IniFile;
        if (!string.IsNullOrWhiteSpace(_legacyConfigFilePath)
            && File.Exists(_legacyConfigFilePath)
            && Load(_legacyConfigFilePath))
        {
            return Save(targetConfigFilePath);
        }

        Base = new BaseConfig();
        return Save();
    }

    static bool MigrateLegacyTheme(BaseConfig config)
    {
        var theme = config.Theme;
        var normalized = AppThemeKeys.Normalize(theme);
        if (string.Equals(theme, normalized, StringComparison.Ordinal))
            return false;

        config.Theme = normalized;
        return true;
    }

    /// <summary>Ensure workspace root exists; map legacy LayoutMode when root is missing.</summary>
    public static bool MigrateWorkspaceLayout(BaseConfig config)
    {
        if (config.WorkspaceRoot is not null)
            return false;

        config.WorkspacePreset = config.LayoutMode == UiLayoutMode.Expanded ? "XL" : "Classic";
        return false;
    }

    static bool MigrateQuickAccessSidebar(BaseConfig config)
    {
        if (config.QuickAccessSidebar is null)
        {
            config.QuickAccessSidebar = new QuickAccessSidebarConfig();
            return true;
        }

        var before = (config.QuickAccessSidebar.ShowLeft, config.QuickAccessSidebar.ShowRight,
            config.QuickAccessSidebar.LegacySidesMigrated);
        config.QuickAccessSidebar.MigrateLegacyDockOnce();
        var after = (config.QuickAccessSidebar.ShowLeft, config.QuickAccessSidebar.ShowRight,
            config.QuickAccessSidebar.LegacySidesMigrated);
        return before != after;
    }

    static bool MigrateGameController(BaseConfig config)
    {
        if (config.GameController is null)
        {
            config.GameController = new GameControllerConfig();
            return true;
        }

        var before = config.GameController.Bindings.Count;
        config.GameController.EnsureDefaultBindings();
        return config.GameController.Bindings.Count != before;
    }

    static bool MigrateThemes(BaseConfig config)
    {
        var migrated = false;

        if (config.CustomThemeDraft is null)
        {
            config.CustomThemeDraft = new AppThemeDefinition
            {
                Name = AppThemeKeys.Custom,
                BaseTheme = AppThemeKeys.Dark,
            };
            migrated = true;
        }

        if (string.IsNullOrWhiteSpace(config.CustomThemeDraft.Name))
        {
            config.CustomThemeDraft.Name = AppThemeKeys.Custom;
            migrated = true;
        }

        if (string.IsNullOrWhiteSpace(config.CustomThemeDraft.BaseTheme))
        {
            config.CustomThemeDraft.BaseTheme = AppThemeKeys.Dark;
            migrated = true;
        }

        config.UserThemes ??= new();
        foreach (var theme in config.UserThemes)
        {
            if (string.IsNullOrWhiteSpace(theme.BaseTheme))
            {
                theme.BaseTheme = AppThemeKeys.Dark;
                migrated = true;
            }
        }

        return migrated;
    }
}
