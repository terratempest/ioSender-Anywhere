using System.Net;
using System.Text;

namespace CNC.Localization;

internal static class CsvLocaleLoader
{
    public static IReadOnlyDictionary<string, string> LoadCulture(string localeRoot, string cultureName)
    {
        var cultureDir = Path.Combine(localeRoot, cultureName, "csv");
        if (!Directory.Exists(cultureDir))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(cultureDir, "*.csv", SearchOption.TopDirectoryOnly))
            LoadFile(file, catalog);

        return catalog;
    }

    private static void LoadFile(string path, Dictionary<string, string> catalog)
    {
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 7)
                continue;

            var value = fields[^1];
            if (string.IsNullOrEmpty(value))
                continue;

            value = DecodeValue(value);
            if (!TryParseKey(fields[0], fields[1], out var assembly, out var page, out var control, out var propertySuffix))
                continue;

            var baseKey = $"{assembly}.{page}.{control}";

            if (!string.IsNullOrEmpty(propertySuffix))
                catalog[$"{baseKey}:{propertySuffix}"] = value;

            // Base key is the visible label (Content/Header/Text), not shortcut ToolTips.
            if (IsPrimaryDisplayProperty(propertySuffix))
            {
                catalog[baseKey] = value;
                catalog[$"{page}.{control}"] = value;
            }

            // WPF-era keys still resolve while controls migrate to Avalonia/Config assemblies.
            if (assembly.Equals("CNC.Controls.Avalonia", StringComparison.OrdinalIgnoreCase)
                || assembly.Equals("CNC.Controls.Config", StringComparison.OrdinalIgnoreCase))
            {
                RegisterWpfAlias(catalog, page, control, propertySuffix, value);
            }
        }
    }

    private static bool TryParseKey(
        string resourcePage,
        string controlProperty,
        out string assembly,
        out string page,
        out string control,
        out string propertySuffix)
    {
        assembly = page = control = propertySuffix = string.Empty;

        var colon = resourcePage.IndexOf(':');
        if (colon < 0)
            return false;

        var resourceName = resourcePage[..colon];
        var pagePart = resourcePage[(colon + 1)..];

        var gMarker = resourceName.IndexOf(".g.", StringComparison.Ordinal);
        assembly = gMarker > 0 ? resourceName[..gMarker] : resourceName.Split('.')[0];

        page = pagePart.EndsWith(".baml", StringComparison.OrdinalIgnoreCase)
            ? pagePart[..^5]
            : pagePart;

        var controlColon = controlProperty.IndexOf(':');
        control = controlColon > 0 ? controlProperty[..controlColon] : controlProperty;
        var property = controlColon > 0 ? controlProperty[(controlColon + 1)..] : string.Empty;

        propertySuffix = string.IsNullOrEmpty(property) ? string.Empty : SimplifyProperty(property);
        return !string.IsNullOrEmpty(control);
    }

    private static void RegisterWpfAlias(
        Dictionary<string, string> catalog,
        string page,
        string control,
        string propertySuffix,
        string value)
    {
        const string wpfAssembly = "CNC.Controls.WPF";
        var wpfKey = $"{wpfAssembly}.{page}.{control}";

        if (!string.IsNullOrEmpty(propertySuffix))
            catalog[$"{wpfKey}:{propertySuffix}"] = value;

        if (IsPrimaryDisplayProperty(propertySuffix))
        {
            catalog[wpfKey] = value;
            catalog[$"{page}.{control}"] = value;
        }
    }

    private static bool IsPrimaryDisplayProperty(string propertySuffix) =>
        string.IsNullOrEmpty(propertySuffix)
        || propertySuffix.Equals("Content", StringComparison.OrdinalIgnoreCase)
        || propertySuffix.Equals("Header", StringComparison.OrdinalIgnoreCase)
        || propertySuffix.Equals("Text", StringComparison.OrdinalIgnoreCase)
        || propertySuffix.Equals("Title", StringComparison.OrdinalIgnoreCase)
        || propertySuffix.Equals("Label", StringComparison.OrdinalIgnoreCase)
        || propertySuffix.Equals("$Content", StringComparison.OrdinalIgnoreCase);

    private static string SimplifyProperty(string property)
    {
        var lastDot = property.LastIndexOf('.');
        return lastDot >= 0 ? property[(lastDot + 1)..] : property;
    }

    private static string DecodeValue(string value)
    {
        if (value.Length == 0)
            return value;

        value = value.Replace("\\n", "\n", StringComparison.Ordinal);
        return WebUtility.HtmlDecode(value);
    }

    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
