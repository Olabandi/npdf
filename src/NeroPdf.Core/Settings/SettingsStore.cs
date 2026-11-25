using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Settings;

/// <summary>User settings. Default values come from Config/defaults.json next to the program.</summary>
public sealed class AppSettings
{
    /// <summary>"Light" or "Dark".</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>Printed documents that were never saved are removed after this many days.</summary>
    public int UnsavedPrintRetentionDays { get; set; }

    /// <summary>The page overview on the left is collapsed.</summary>
    public bool SidebarCollapsed { get; set; }
}

/// <summary>Loads the defaults, overlays the user's settings file and saves changes.</summary>
public sealed class SettingsStore(string defaultsPath, string userPath, ILogger<SettingsStore> logger)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        var merged = ReadObject(defaultsPath) ?? new JsonObject();
        if (ReadObject(userPath) is { } user)
        {
            foreach (var (key, value) in user)
            {
                merged[key] = value?.DeepClone();
            }
        }
        var settings = merged.Deserialize<AppSettings>() ?? new AppSettings();
        logger.LogDebug("Settings loaded, theme {Theme}", settings.Theme);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
            var temp = userPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, userPath, overwrite: true);
            logger.LogDebug("Settings saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Settings could not be saved");
        }
    }

    private JsonObject? ReadObject(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Settings file {File} ignored", Path.GetFileName(path));
            return null;
        }
    }
}
