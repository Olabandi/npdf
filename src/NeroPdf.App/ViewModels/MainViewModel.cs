using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Pdf;

namespace NeroPdf.App.ViewModels;

public enum ViewState
{
    Empty,
    Loading,
    Showing,
    Error,
}

/// <summary>State of the main window: the open document, its pages and the theme switch.</summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPdfEngine _engine;
    private readonly Action<bool> _applyDarkTheme;
    private readonly TimeSpan _progressDelay;
    private readonly ILogger<MainViewModel> _logger;

    private IPdfDocument? _document;
    private ViewState _state = ViewState.Empty;
    private string? _documentName;
    private string _errorMessage = string.Empty;
    private int _currentPage = 1;
    private bool _isDark;

    public MainViewModel(IPdfEngine engine, bool isDark, Action<bool> applyDarkTheme, TimeSpan progressDelay,
        ILogger<MainViewModel> logger)
    {
        _engine = engine;
        _isDark = isDark;
        _applyDarkTheme = applyDarkTheme;
        _progressDelay = progressDelay;
        _logger = logger;
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    /// <summary>Raised once when the first page of a document has been drawn (start time measurement).</summary>
    public event EventHandler? FirstPageShown;

    public ObservableCollection<PageViewModel> Pages { get; } = [];

    public ICommand ToggleThemeCommand { get; }

    public ViewState State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(HasPages));
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public bool IsEmpty => State == ViewState.Empty;

    public bool IsLoading => State == ViewState.Loading;

    public bool HasPages => State == ViewState.Showing;

    public bool IsError => State == ViewState.Error;

    /// <summary>File name shown in the header; only for opened files, not for printed documents.</summary>
    public string? DocumentName
    {
        get => _documentName;
        private set
        {
            if (Set(ref _documentName, value))
            {
                OnPropertyChanged(nameof(HasDocumentName));
            }
        }
    }

    public bool HasDocumentName => !string.IsNullOrEmpty(DocumentName);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => Set(ref _errorMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (Set(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageIndicator));
            }
        }
    }

    public string PageIndicator => Texts.Format("Viewer.PageIndicator", CurrentPage, Pages.Count);

    public bool IsDark
    {
        get => _isDark;
        private set
        {
            if (Set(ref _isDark, value))
            {
                OnPropertyChanged(nameof(ThemeToggleTooltip));
            }
        }
    }

    public string ThemeToggleTooltip => Texts.Get(IsDark ? "Theme.SwitchToLight" : "Theme.SwitchToDark");

    /// <summary>Opens a document in the background. The progress display only appears for longer loads.</summary>
    public async Task OpenAsync(string path, bool showFileName)
    {
        _logger.LogInformation("Opening document (from {Source})", showFileName ? "file" : "printer");
        var watch = Stopwatch.StartNew();
        CloseDocument();
        DocumentName = showFileName ? Path.GetFileName(path) : null;

        var opening = Task.Run(() =>
        {
            var result = _engine.Open(path);
            var sizes = result.Document is { } doc
                ? Enumerable.Range(0, doc.PageCount).Select(doc.GetPageSize).ToArray()
                : [];
            return (result, sizes);
        });
        if (await Task.WhenAny(opening, Task.Delay(_progressDelay)) != opening)
        {
            State = ViewState.Loading;
        }

        var (result, sizes) = await opening;
        if (result.Status != PdfOpenStatus.Ok || result.Document is null)
        {
            ErrorMessage = Texts.Format(ErrorTextId(result.Status), Path.GetFileName(path));
            State = ViewState.Error;
            _logger.LogInformation("Document not shown: {Status}", result.Status);
            return;
        }

        _document = result.Document;
        for (var i = 0; i < sizes.Length; i++)
        {
            Pages.Add(new PageViewModel(_document, i, sizes[i]));
        }
        CurrentPage = 1;
        OnPropertyChanged(nameof(PageIndicator));
        State = ViewState.Showing;
        _logger.LogInformation("Document ready: {Pages} pages in {Ms} ms", Pages.Count, watch.ElapsedMilliseconds);
    }

    /// <summary>Called by the page view when a page bitmap is on screen.</summary>
    public void NotifyPageShown(int index)
    {
        if (index == 0 && FirstPageShown is { } handler)
        {
            FirstPageShown = null;
            Dispatcher.UIThread.Post(() => handler(this, EventArgs.Empty));
        }
    }

    public void Dispose() => CloseDocument();

    private void CloseDocument()
    {
        Pages.Clear();
        _document?.Dispose();
        _document = null;
        State = ViewState.Empty;
    }

    private void ToggleTheme()
    {
        IsDark = !IsDark;
        _applyDarkTheme(IsDark);
    }

    private static string ErrorTextId(PdfOpenStatus status) => status switch
    {
        PdfOpenStatus.NotFound => "Error.NotFound",
        PdfOpenStatus.Damaged => "Error.Damaged",
        PdfOpenStatus.PasswordRequired or PdfOpenStatus.WrongPassword => "Error.PasswordRequired",
        PdfOpenStatus.Unsupported => "Error.Unsupported",
        _ => "Error.Failed",
    };
}
