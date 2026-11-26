using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;

namespace NeroPdf.App.ViewModels;

/// <summary>The two tiles linking to other Nero products. The addresses come from Config/links.json.</summary>
public sealed class PromoLinksViewModel
{
    private readonly IReadOnlyDictionary<string, string> _links;
    private readonly ILogger<PromoLinksViewModel> _logger;

    public PromoLinksViewModel(string linksFile, ILogger<PromoLinksViewModel> logger)
    {
        _logger = logger;
        _links = Load(linksFile, logger);
        OpenNeroAiCommand = new RelayCommand(() => Open("NeroAi"));
        OpenNeroPlatinumCommand = new RelayCommand(() => Open("NeroPlatinum"));
    }

    /// <summary>Opens an address in the default browser; set by the window.</summary>
    public Func<Uri, Task<bool>>? Launcher { get; set; }

    public ICommand OpenNeroAiCommand { get; }

    public ICommand OpenNeroPlatinumCommand { get; }

    private async void Open(string key)
    {
        if (Launcher is null || !_links.TryGetValue(key, out var address) || !Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Link {Key} not available", key);
            return;
        }
        try
        {
            var opened = await Launcher(uri);
            _logger.LogInformation("Product link {Key} opened: {Opened}", key, opened);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product link {Key} could not be opened", key);
        }
    }

    private static Dictionary<string, string> Load(string file, ILogger logger)
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), AppJsonContext.Default.DictionaryStringString) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Link file could not be loaded");
            return [];
        }
    }
}
