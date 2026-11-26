using System.Text.Json;
using Microsoft.Win32;
using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Simple check whether Nero Start / Nero Platinum is installed: the Nero installer registers each
/// component under "neropack\&lt;number&gt;", with the installed files as value names. The keys come
/// from Config/nero-suite.json. Both registry views are checked (the Nero installer is 32-bit, so its
/// keys usually sit under WOW6432Node).
/// macOS counterpart: none yet; scanning and converting stay locked there.
/// </summary>
internal sealed class WindowsNeroSuite : INeroSuite
{
    private static readonly string ConfigFile = Path.Combine(AppContext.BaseDirectory, "Config", "nero-suite.json");

    public bool IsInstalled()
    {
        foreach (var key in ReadKeys())
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                if (KeyPointsToExistingFile(view, key))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool KeyPointsToExistingFile(RegistryView view, string path)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(path);
            return key is not null && key.GetValueNames().Any(name => name.Length > 0 && File.Exists(name));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static List<string> ReadKeys()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ConfigFile));
            return document.RootElement.GetProperty("RegistryKeys").EnumerateArray()
                .Select(e => e.GetString()).OfType<string>().ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
