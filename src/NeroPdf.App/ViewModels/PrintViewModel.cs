using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Platform;

namespace NeroPdf.App.ViewModels;

/// <summary>"Print": the document as shown (with page changes, saved or not) on a real printer.</summary>
public sealed class PrintViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IDocumentPrinter _printer;
    private readonly ILogger<PrintViewModel> _logger;
    private bool _isPrinting;
    private string _progressText = string.Empty;

    public PrintViewModel(MainViewModel main, IDocumentPrinter printer, ILogger<PrintViewModel> logger)
    {
        _main = main;
        _printer = printer;
        _logger = logger;
        PrintCommand = new RelayCommand(() => _main.Run(PrintAsync));
        _main.PropertyChanged += OnMainChanged;
    }

    public ICommand PrintCommand { get; }

    public bool CanPrint => _printer.IsSupported && _main.HasPages && !IsPrinting;

    public bool IsPrinting
    {
        get => _isPrinting;
        private set
        {
            if (Set(ref _isPrinting, value))
            {
                OnPropertyChanged(nameof(CanPrint));
            }
        }
    }

    /// <summary>"Printing page X of Y …" while the pages go to the printer.</summary>
    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasPages))
        {
            OnPropertyChanged(nameof(CanPrint));
        }
    }

    private async Task PrintAsync()
    {
        var dialogs = _main.Dialogs;
        var document = _main.Document;
        if (!CanPrint || dialogs is null || document is null)
        {
            return;
        }
        // The system dialog belongs to the window and runs on the user interface thread.
        using var job = _printer.AskForPrintJob(dialogs.WindowHandle, document.PageCount);
        if (job is null)
        {
            return;
        }
        var title = _main.DocumentName is { } name ? Path.GetFileNameWithoutExtension(name) : Texts.Get("Print.DefaultName");
        var total = job.Pages.Count;
        _logger.LogInformation("Printing {Pages} pages", total);
        IsPrinting = true;
        ProgressText = Texts.Format("Print.Progress", 1, total);
        var progress = new Progress<int>(done => ProgressText = Texts.Format("Print.Progress", Math.Min(done + 1, total), total));
        try
        {
            await Task.Run(() => job.Print(document, title, progress));
            _logger.LogInformation("Printed {Pages} pages", total);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            _logger.LogError(ex, "Printing failed");
            await dialogs.ShowMessageAsync(Texts.Get("Print.Failed.Title"), Texts.Get("Print.Failed.Text"));
        }
        finally
        {
            IsPrinting = false;
        }
    }
}
