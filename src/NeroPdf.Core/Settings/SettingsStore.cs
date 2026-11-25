using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Settings;

/// <summary>User settings. Default values come from Config/defaults.json next to the program.</summary>
public sealed class AppSettings
{
    /// <summary>"Light" or "Dark".</summary>
    public string Theme { get; set; } = "Light";
}

/// <summary>Loads the defaults, overlays the user's settings file and saves changes.</summary>
public sealed class SettingsStore(string defaultsPath, string userPath, ILogger<SettingsStore> logger)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        var settings = ReadFile(defaultsPath) ?? new AppSettings();
        var user = ReadFile(userPath);
        if (user is not null)
        {
            settings = user;
        }
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

    private AppSettings? ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Settings file {File} ignored", Path.GetFileName(path));
            return null;
        }
    }
}
