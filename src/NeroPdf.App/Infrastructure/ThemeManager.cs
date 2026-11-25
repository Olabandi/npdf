using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;

namespace NeroPdf.App.Infrastructure;

/// <summary>
/// Loads colours (Themes/*.json) and sizes (Config/layout.json) into the application resources.
/// Nothing of this is hard-coded in XAML.
/// </summary>
public sealed class ThemeManager
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private readonly Application _application;
    private readonly string _baseDirectory;
    private readonly ILogger<ThemeManager> _logger;

    public ThemeManager(Application application, string baseDirectory, ILogger<ThemeManager> logger)
    {
        _application = application;
        _baseDirectory = baseDirectory;
        _logger = logger;
        Layout = LoadLayout();
    }

    public IReadOnlyDictionary<string, double> Layout { get; }

    public double LayoutValue(string key, double fallback = 0) =>
        Layout.TryGetValue(key, out var value) ? value : fallback;

    /// <summary>Puts the layout sizes and derived values into the resources.</summary>
    public void ApplyLayout(Core.Platform.HeaderInsets insets)
    {
        var resources = _application.Resources;
        foreach (var (key, value) in Layout)
        {
            resources["Layout." + key] = value;
        }

        var logo = LayoutValue("LogoHeight", 40);
        resources["HeaderPadding"] = new Thickness(LayoutValue("HeaderPaddingLeft") + insets.Left, 0,
            LayoutValue("HeaderPaddingRight") + insets.Right, 0);
        // "PDF" next to the logo: cap height about two thirds of the speech bubble body, centred on the body.
        resources["PdfFontSize"] = logo * 0.78;
        resources["PdfTextMargin"] = new Thickness(logo * 0.3, 0, 0, 0);
        // Body of the speech bubble without its tail (520 of 627 pixels in the logo image).
        resources["LogoBodyHeight"] = logo * 520 / 627;
        // The capital letters sit slightly below the middle of the text box; lift them a little.
        resources["PdfTextOffset"] = new Thickness(0, 0, 0, logo * 0.78 * 0.18);
        resources["FileNameMargin"] = new Thickness(LayoutValue("FileNameSpacing"), 0, 0, 0);
        resources["FileNamePadding"] = new Thickness(LayoutValue("FileNameSpacing"), 0, 0, 0);
        resources["PageItemMargin"] = new Thickness(0, 0, 0, LayoutValue("PageSpacing"));
        resources["PageListMargin"] = new Thickness(LayoutValue("PageMargin"));
        resources["PillMargin"] = new Thickness(0, 0, 0, LayoutValue("PillMarginBottom"));
        resources["ModeButtonMargin"] = new Thickness(0, 0, LayoutValue("ModeButtonMargin"), LayoutValue("ModeButtonMargin"));
        resources["ModeButtonCornerRadius"] = new CornerRadius(LayoutValue("ModeButtonSize") / 2);
        resources["PillCornerRadius"] = new CornerRadius(LayoutValue("PillCornerRadius"));
        resources["PillPadding"] = new Thickness(LayoutValue("PillPaddingHorizontal"), LayoutValue("PillPaddingVertical"));
        resources["HeaderButtonsMargin"] = new Thickness(LayoutValue("HeaderButtonsGap"), 0, 0, 0);
        resources["HeaderButtonPadding"] = new Thickness(LayoutValue("HeaderButtonPaddingHorizontal"), LayoutValue("HeaderButtonPaddingVertical"));
        resources["HeaderButtonCornerRadius"] = new CornerRadius(LayoutValue("HeaderButtonCornerRadius"));
        resources["DialogPadding"] = new Thickness(LayoutValue("DialogPadding"));
        resources["HintPadding"] = new Thickness(LayoutValue("HintPaddingHorizontal"), LayoutValue("HintPaddingVertical"));
        resources["SidebarPadding"] = new Thickness(LayoutValue("SidebarPadding"));
        resources["SidebarHeaderMargin"] = new Thickness(LayoutValue("SidebarPadding"), LayoutValue("SidebarPadding") / 2, LayoutValue("SidebarPadding") / 2, 0);
        resources["SidebarTogglePadding"] = new Thickness(LayoutValue("SidebarTogglePadding"));
        resources["TileCollapsedPadding"] = new Thickness(Math.Max(0, (LayoutValue("SidebarCollapsedWidth") - 2 * LayoutValue("SidebarPadding") - LayoutValue("TileIconSize") - 2) / 2), LayoutValue("TilePaddingVertical"));
        resources["ThumbnailItemMargin"] = new Thickness(0, 0, 0, LayoutValue("ThumbnailSpacing"));
        resources["TilePadding"] = new Thickness(LayoutValue("TilePaddingHorizontal"), LayoutValue("TilePaddingVertical"));
        resources["TileCornerRadius"] = new CornerRadius(LayoutValue("TileCornerRadius"));
        resources["ThumbnailCheckMargin"] = new Thickness(0, 4, 4, 0);
        resources["ThumbnailCheckCornerRadius"] = new CornerRadius(LayoutValue("ThumbnailCheckSize") / 2);
        resources["ThumbnailToolPadding"] = new Thickness(LayoutValue("ThumbnailToolPadding"));
        resources["DropGapFrameMargin"] = new Thickness(0, 4, 0, LayoutValue("ThumbnailSpacing"));
        resources["DragCountPadding"] = new Thickness(6, 1);
        resources["TileTextMargin"] = new Thickness(LayoutValue("TilePaddingHorizontal"), 0, 0, 0);
        resources["HintButtonsMargin"] = new Thickness(LayoutValue("HintButtonsGap"), 0, 0, 0);
    }

    /// <summary>Switches colours and the Fluent theme variant.</summary>
    public void ApplyTheme(string name)
    {
        var path = Path.Combine(_baseDirectory, "Themes", name.ToLowerInvariant() + ".json");
        ThemeFile? theme = null;
        try
        {
            theme = JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Theme {Theme} could not be loaded", name);
        }
        if (theme?.Colors is null)
        {
            return;
        }

        var resources = _application.Resources;
        foreach (var (key, value) in theme.Colors)
        {
            if (Color.TryParse(value, out var color))
            {
                resources[key] = new SolidColorBrush(color);
                resources[key + "Color"] = color;
            }
            else
            {
                _logger.LogWarning("Theme {Theme}: colour {Key} has an invalid value", name, key);
            }
        }
        if (resources.TryGetResource("PageShadowColor", null, out var shadow) && shadow is Color shadowColor)
        {
            resources["PageBoxShadow"] = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 18, Color = shadowColor });
        }
        _application.RequestedThemeVariant = theme.Variant == Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        _logger.LogInformation("Theme applied: {Theme}", name);
    }

    private Dictionary<string, double> LoadLayout()
    {
        var path = Path.Combine(_baseDirectory, "Config", "layout.json");
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Layout file could not be loaded");
            return [];
        }
    }

    private sealed class ThemeFile
    {
        public string Variant { get; set; } = Light;

        public Dictionary<string, string>? Colors { get; set; }
    }
}
