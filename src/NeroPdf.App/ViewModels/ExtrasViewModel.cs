using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Platform;
using NeroPdf.Core.Settings;

namespace NeroPdf.App.ViewModels;

/// <summary>
/// "Scan …" and "Convert …" in the header. Both are only usable when a Nero suite (Nero Start /
/// Nero Platinum) is installed; otherwise they are covered by "Get Nero Platinum to unlock".
/// </summary>
public sealed class ExtrasViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly INeroSuite _suite;
    private readonly IFileConverter _converter;
    private readonly IScanner _scanner;
    private readonly IPrintInbox _inbox;
    private readonly IPdfEngine _engine;
    private readonly int _scanDpi;
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private ConvertibleTypes _types = ConvertibleTypes.None;
    private readonly ILogger<ExtrasViewModel> _logger;
    private bool _isUnlocked;
    private bool _checked;

    public ExtrasViewModel(MainViewModel main, INeroSuite suite, IFileConverter converter, IScanner scanner,
        IPrintInbox inbox, IPdfEngine engine, AppSettings settings, Action saveSettings, ICommand unlockCommand,
        ILogger<ExtrasViewModel> logger)
    {
        _main = main;
        _suite = suite;
        _converter = converter;
        _scanner = scanner;
        _inbox = inbox;
        _engine = engine;
        _settings = settings;
        _saveSettings = saveSettings;
        _scanDpi = settings.ScanDpi > 0 ? settings.ScanDpi : DefaultScanDpi;
        _logger = logger;
        UnlockCommand = unlockCommand;
        // Locked, both buttons lead to the Nero Platinum page.
        ScanCommand = new RelayCommand(() => RunOrOfferUnlock(ScanAsync));
        ConvertCommand = new RelayCommand(() => RunOrOfferUnlock(ConvertAsync));
    }

    private const int DefaultScanDpi = 300;

    private void RunOrOfferUnlock(Func<Task> action)
    {
        if (IsUnlocked)
        {
            _main.Run(action);
        }
        else
        {
            _logger.LogInformation("Locked function clicked, Nero Platinum page offered");
            UnlockCommand.Execute(null);
        }
    }

    public ICommand ScanCommand { get; }

    public ICommand ConvertCommand { get; }

    /// <summary>Opens the Nero Platinum page.</summary>
    public ICommand UnlockCommand { get; }

    public bool IsUnlocked
    {
        get => _isUnlocked;
        private set
        {
            if (Set(ref _isUnlocked, value))
            {
                OnPropertyChanged(nameof(IsLocked));
            }
        }
    }

    public bool IsLocked => !IsUnlocked;

    /// <summary>Checks in the background whether the Nero suite is installed (at start and when the window is activated).</summary>
    public async Task RefreshAsync()
    {
        try
        {
            var installed = await Task.Run(_suite.IsInstalled);
            if (installed != IsUnlocked || !_checked)
            {
                _checked = true;
                _logger.LogInformation("Scan and convert {State}", installed ? "unlocked" : "locked");
            }
            IsUnlocked = installed;
            if (installed && _converter.IsSupported && _types == ConvertibleTypes.None)
            {
                // Needed for drag and drop; reading the file associations takes a moment.
                _types = await Task.Run(_converter.GetConvertibleTypes);
                _logger.LogDebug("{Images} image and {Documents} document types can be converted", _types.Images.Count, _types.Documents.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nero suite check failed");
        }
    }

    private async Task ConvertAsync()
    {
        var dialogs = _main.Dialogs;
        if (!IsUnlocked || dialogs is null)
        {
            return;
        }
        if (!_converter.IsSupported)
        {
            await dialogs.ShowMessageAsync(Texts.Get("Convert.Failed.Title"), Texts.Get("Convert.NoPrinter"));
            return;
        }
        _types = await Task.Run(_converter.GetConvertibleTypes);
        var pick = await dialogs.PickFileToConvertAsync(_types, _settings.ConvertFilter, _settings.ConvertFolder);
        if (pick is null)
        {
            return;
        }
        _settings.ConvertFilter = pick.Filter;
        _settings.ConvertFolder = Path.GetDirectoryName(pick.Path);
        _saveSettings();
        await ConvertFileAsync(pick.Path);
    }

    /// <summary>True for files that can be converted (used for drag and drop); always false while locked.</summary>
    public bool CanConvert(string path) => IsUnlocked && _types.Contains(path);

    /// <summary>Converts a file dropped onto the window.</summary>
    public void ConvertDropped(string path) => _main.Run(() => ConvertFileAsync(path));

    private async Task ConvertFileAsync(string path)
    {
        var dialogs = _main.Dialogs;
        if (!IsUnlocked || dialogs is null)
        {
            return;
        }
        var folder = NewWorkFolder("Convert");
        try
        {
            var outcome = await Task.Run(() => _converter.Convert(path, folder));
            _logger.LogInformation("Convert ({Extension}): {Result}", Path.GetExtension(path), outcome.Result);
            if (outcome is { Result: ConvertResult.Pages, Pages.Count: > 0 })
            {
                var title = Path.GetFileNameWithoutExtension(path);
                await _main.CreateDocumentAsync(Texts.Get("Viewer.Loading"), () => CreatePdfAsync(outcome.Pages, title));
                return;
            }
            var textId = outcome.Result switch
            {
                ConvertResult.Printing => null,
                ConvertResult.NoPrinter => "Convert.NoPrinter",
                ConvertResult.NotConvertible => "Convert.NotPrintable",
                _ => "Convert.Failed.Text",
            };
            if (textId is not null)
            {
                await dialogs.ShowMessageAsync(Texts.Get("Convert.Failed.Title"), Texts.Format(textId, Path.GetFileName(path)));
            }
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private async Task ScanAsync()
    {
        var dialogs = _main.Dialogs;
        if (!IsUnlocked || dialogs is null)
        {
            return;
        }
        var devices = await Task.Run(_scanner.GetDevices);
        _logger.LogInformation("{Count} scanners found", devices.Count);
        if (devices.Count == 0)
        {
            await dialogs.ShowMessageAsync(Texts.Get("Scan.NoScanner.Title"), Texts.Get("Scan.NoScanner.Text"));
            return;
        }
        var request = await dialogs.AskScanSettingsAsync(devices);
        if (request is null)
        {
            return;
        }
        var settings = new ScanSettings(request.Source, request.Color, _scanDpi);
        await _main.CreateDocumentAsync(Texts.Get("Scan.Progress"), () => ScanPagesAsync(request.Device, settings), scanning: true);
    }

    /// <summary>Scans until the user is done and returns the new PDF, or null if nothing was scanned.</summary>
    private async Task<string?> ScanPagesAsync(ScannerDevice device, ScanSettings settings)
    {
        var dialogs = _main.Dialogs!;
        var folder = NewWorkFolder("Scan");
        var pages = new List<ScannedImage>();
        try
        {
            while (true)
            {
                try
                {
                    var batchFolder = Path.Combine(folder, (pages.Count + 1).ToString("D4"));
                    var before = pages.Count;
                    _main.LoadingText = ScanningText(before);
                    // Created here, so the reports arrive on the user interface thread.
                    var progress = new Progress<int>(count => _main.LoadingText = ScanningText(before + count));
                    var batch = await Task.Run(() => _scanner.Scan(device, settings, batchFolder, progress));
                    pages.AddRange(batch);
                    _logger.LogInformation("Scanned {Count} pages ({Source}, {Color})", batch.Count, settings.Source, settings.Color);
                }
                catch (ScanException ex) when (ex.Error == ScanError.Busy)
                {
                    // Keep what the feeder already delivered; "Try again" continues with the next sheets.
                    pages.AddRange(ex.Pages);
                    _logger.LogWarning("Scanner busy after {Pages} pages", pages.Count);
                    if (await dialogs.AskRetryAsync(Texts.Get("Scan.Failed.Title"), Texts.Get("Scan.Failed.Busy")))
                    {
                        continue;
                    }
                    if (pages.Count == 0)
                    {
                        return null;
                    }
                }
                catch (ScanException ex)
                {
                    pages.AddRange(ex.Pages);
                    _logger.LogWarning(ex, "Scan failed: {Error}", ex.Error);
                    var textId = ex.Error switch
                    {
                        ScanError.NoPaper => "Scan.Failed.NoPaper",
                        ScanError.Offline => "Scan.Failed.Offline",
                        _ => "Scan.Failed.Text",
                    };
                    await dialogs.ShowMessageAsync(Texts.Get("Scan.Failed.Title"), Texts.Get(textId));
                    if (pages.Count == 0)
                    {
                        return null;
                    }
                }
                // From the feeder all sheets come at once; on the flatbed one page at a time.
                if (settings.Source == ScanSource.Feeder || !await dialogs.AskScanAnotherPageAsync(pages.Count))
                {
                    break;
                }
            }
            if (pages.Count == 0)
            {
                return null;
            }
            return await CreatePdfAsync(pages, Texts.Get("Scan.DocumentName"));
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>Makes the PDF from image pages; it is treated like a printed document. Null on failure (after a message).</summary>
    private async Task<string?> CreatePdfAsync(IReadOnlyList<ScannedImage> pages, string title)
    {
        var target = _inbox.CreateDocumentPath(title);
        try
        {
            await Task.Run(() => WritePdf(pages, target));
            return target;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "PDF could not be created from images");
            await _main.Dialogs!.ShowMessageAsync(Texts.Get("Convert.Failed.Title"), Texts.Format("Convert.Failed.Text", title));
            return null;
        }
    }

    private static string ScanningText(int pages) =>
        pages > 0 ? Texts.Format("Scan.ProgressPages", pages) : Texts.Get("Scan.Progress");

    private static string NewWorkFolder(string kind) =>
        Path.Combine(Path.GetTempPath(), "NeroPdf", kind + "-" + Guid.NewGuid().ToString("N"));

    private void WritePdf(IReadOnlyList<ScannedImage> pages, string target)
    {
        // Written under a temporary name first, so a half-written file never counts as a document.
        var partial = target + PrintFolders.PartialExtension;
        try
        {
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                _engine.CreateFromImages(pages.Select(p => new PdfImagePage(p.JpegPath, p.Dpi)).ToList(), output);
            }
            File.Move(partial, target);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Scan images could not be removed");
        }
    }
}

/// <summary>What the user chose in the scan dialog.</summary>
public sealed record ScanRequest(ScannerDevice Device, ScanSource Source, ScanColor Color);
