using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Localization;

/// <summary>
/// Central access to all visible texts. One JSON file per language with stable text ids.
/// The available languages follow from the files present; there is no language list in code.
/// </summary>
public sealed class Localizer
{
    public const string FallbackCulture = "en";

    private readonly IReadOnlyDictionary<string, string> _active;
    private readonly IReadOnlyDictionary<string, string> _fallback;
    private readonly ILogger _logger;

    private Localizer(string cultureName, IReadOnlyDictionary<string, string> active,
        IReadOnlyDictionary<string, string> fallback, ILogger logger)
    {
        CultureName = cultureName;
        _active = active;
        _fallback = fallback;
        _logger = logger;
    }

    /// <summary>Name of the language file in use, e.g. "de" or "en-GB".</summary>
    public string CultureName { get; }

    /// <summary>
    /// Loads the best file for <paramref name="culture"/>: the exact culture, then its parent
    /// cultures (e.g. nb-NO → nb, zh-TW → zh-Hant → zh), finally English.
    /// </summary>
    public static Localizer Load(string directory, CultureInfo culture, ILogger logger)
    {
        var files = LanguageFiles.Find(directory);
        var fallback = files.TryGetValue(FallbackCulture, out var fallbackPath)
            ? LanguageFiles.Read(fallbackPath)
            : new Dictionary<string, string>();
        if (fallback.Count == 0)
        {
            logger.LogError("Fallback language file '{Culture}' missing or empty in language directory", FallbackCulture);
        }

        var chosen = SelectCulture(files.Keys, culture);
        var active = chosen == FallbackCulture ? fallback : LanguageFiles.Read(files[chosen]);
        logger.LogInformation("Language selected: {Chosen} (system culture {System}, {Count} languages available)",
            chosen, culture.Name, files.Count);
        return new Localizer(chosen, active, fallback, logger);
    }

    /// <summary>Picks the language file name for a culture, walking up the parent chain.</summary>
    public static string SelectCulture(IEnumerable<string> available, CultureInfo culture)
    {
        var set = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (set.TryGetValue(c.Name, out var match))
            {
                return match;
            }
        }
        return FallbackCulture;
    }

    /// <summary>Returns the text for <paramref name="id"/>; falls back to English, then to the id itself.</summary>
    public string Get(string id)
    {
        if (_active.TryGetValue(id, out var text) || _fallback.TryGetValue(id, out text))
        {
            return text;
        }
        _logger.LogWarning("Missing text id {Id}", id);
        return id;
    }

    /// <summary>Returns the text for <paramref name="id"/> with {0}, {1} … filled in.</summary>
    public string Format(string id, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(id), args);
}

/// <summary>Reads the language files; also used by the test that checks for missing keys.</summary>
public static class LanguageFiles
{
    public static IReadOnlyDictionary<string, string> Find(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                result[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }
        return result;
    }

    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, CoreJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();
    }
}
