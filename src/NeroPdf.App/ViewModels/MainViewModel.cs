using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Dialogs;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Documents;
using NeroPdf.Core.Pdf;

namespace NeroPdf.App.ViewModels;

public enum ViewState
{
    Empty,
    Loading,
    Showing,
    Error,
}

/// <summary>
/// State of the main window: opening, closing and saving the document, the theme and the page
/// overview panel. The pages themselves are handled by <see cref="PageOverviewViewModel"/>.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPdfEngine _engine;
    private readonly DocumentSaver _saver;
    private readonly Action<bool> _applyDarkTheme;
    private readonly TimeSpan _progressDelay;
    private readonly ILogger<MainViewModel> _logger;

    private IPdfDocument? _document;
    private string? _sourcePath;
    private bool _isPrinted;
    private bool _hasChanges;
    private bool _busy;
    private ViewState _state = ViewState.Empty;
    private string? _documentName;
    private string _errorMessage = string.Empty;
    private bool _isDark;
    private bool _isSidebarExpanded = true;
    private string _loadingText = Texts.Get("Viewer.Loading");

    public MainViewModel(IPdfEngine engine, DocumentSaver saver, PageOverviewViewModel overview, bool isDark,
        Action<bool> applyDarkTheme, TimeSpan progressDelay, ILogger<MainViewModel> logger)
    {
        _engine = engine;
        _saver = saver;
        Overview = overview;
        _isDark = isDark;
        _applyDarkTheme = applyDarkTheme;
        _progressDelay = progressDelay;
        _logger = logger;
        Overview.DocumentChanged += (_, _) =>
        {
            _hasChanges = true;
            OnDocumentStateChanged();
        };
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        OpenCommand = new RelayCommand(() => Run(OpenWithDialogAsync));
        SaveCommand = new RelayCommand(() => Run(async () => await SaveAsync()));
        SaveAsCommand = new RelayCommand(() => Run(SaveAsAsync));
    }

    /// <summary>Raised once when the first page of a document has been drawn (start time measurement).</summary>
    public event EventHandler? FirstPageShown;

    public PageOverviewViewModel Overview { get; }

    /// <summary>Tiles linking to other Nero products.</summary>
    public PromoLinksViewModel? PromoLinks { get; set; }

    /// <summary>Scanning and converting (unlocked with Nero Platinum).</summary>
    public ExtrasViewModel? Extras { get; set; }

    /// <summary>The hint to make Nero PDF the default PDF app.</summary>
    public DefaultAppHintViewModel? DefaultAppHint { get; set; }

    /// <summary>Set by the window; used for file pickers and questions.</summary>
    public IDialogs? Dialogs { get; set; }

    public ICommand ToggleThemeCommand { get; }

    public ICommand ToggleSidebarCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SaveAsCommand { get; }

    /// <summary>The page overview is expanded; when collapsed only the product tiles remain as icons.</summary>
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (Set(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(ShowThumbnails));
                OnPropertyChanged(nameof(SidebarToggleTooltip));
            }
        }
    }

    public bool ShowThumbnails => HasPages && IsSidebarExpanded;

    public string SidebarToggleTooltip => Texts.Get(IsSidebarExpanded ? "Sidebar.Collapse" : "Sidebar.Expand");

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
                OnPropertyChanged(nameof(ShowThumbnails));
                OnDocumentStateChanged();
            }
        }
    }

    /// <summary>Text above the progress bar: loading a document or, for example, scanning.</summary>
    public string LoadingText
    {
        get => _loadingText;
        private set => Set(ref _loadingText, value);
    }

    public bool IsEmpty => State == ViewState.Empty;

    public bool IsLoading => State == ViewState.Loading;

    public bool HasPages => State == ViewState.Showing;

    public bool IsError => State == ViewState.Error;

    /// <summary>A printed document that was never saved, or an opened file with page changes.</summary>
    public bool HasUnsavedChanges => HasPages && (_isPrinted || _hasChanges);

    public bool CanSave => HasUnsavedChanges;

    /// <summary>"Save as" is offered for opened files.</summary>
    public bool CanSaveAs => HasPages && !_isPrinted;

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

    /// <summary>Opens a file chosen by the user (dialog or drag and drop) after confirming unsaved changes.</summary>
    public async Task OpenFileAsync(string path)
    {
        if (await ConfirmCloseAsync())
        {
            await OpenAsync(path, printed: false);
        }
    }

    /// <summary>
    /// Opens a document in the background; asks for a password if needed. The progress display only
    /// appears for longer loads.
    /// </summary>
    public async Task OpenAsync(string path, bool printed)
    {
        _logger.LogInformation("Opening document (from {Source})", printed ? "printer" : "file");
        var watch = Stopwatch.StartNew();
        CloseDocument();
        var fileName = Path.GetFileName(path);

        string? password = null;
        while (true)
        {
            var result = await OpenInBackgroundAsync(path, password);
            if (result.Status is PdfOpenStatus.PasswordRequired or PdfOpenStatus.WrongPassword && Dialogs is not null)
            {
                State = ViewState.Empty;
                password = await Dialogs.AskPasswordAsync(fileName, result.Status == PdfOpenStatus.WrongPassword);
                if (password is not null)
                {
                    continue;
                }
                _logger.LogInformation("Password entry cancelled");
            }
            password = null;

            if (result.Status != PdfOpenStatus.Ok || result.Document is null)
            {
                ErrorMessage = Texts.Format(ErrorTextId(result.Status), fileName);
                DocumentName = printed ? null : fileName;
                State = ViewState.Error;
                _logger.LogInformation("Document not shown: {Status}", result.Status);
                return;
            }

            _document = result.Document;
            _sourcePath = path;
            _isPrinted = printed;
            _hasChanges = false;
            DocumentName = printed ? null : fileName;
            Overview.Load(_document);
            State = ViewState.Showing;
            _logger.LogInformation("Document ready: {Pages} pages in {Ms} ms", Overview.Pages.Count, watch.ElapsedMilliseconds);
            return;
        }
    }

    /// <summary>
    /// Creates a new document (a scan) after confirming unsaved changes and shows it like a printed one:
    /// temporary until saved. <paramref name="create"/> returns the file, or null if nothing was created.
    /// </summary>
    public async Task CreateDocumentAsync(string progressText, Func<Task<string?>> create)
    {
        if (!await ConfirmCloseAsync())
        {
            return;
        }
        CloseDocument();
        LoadingText = progressText;
        State = ViewState.Loading;
        string? path = null;
        try
        {
            path = await create();
        }
        finally
        {
            LoadingText = Texts.Get("Viewer.Loading");
            if (path is null)
            {
                State = ViewState.Empty;
            }
        }
        if (path is not null)
        {
            await OpenAsync(path, printed: true);
        }
    }

    /// <summary>
    /// Asks what to do with an unsaved printout or unsaved changes before the document is closed or
    /// replaced. Returns false if the user wants to keep it open.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (!HasUnsavedChanges || Dialogs is null)
        {
            return true;
        }
        var choice = _isPrinted
            ? await Dialogs.AskUnsavedPrintAsync()
            : await Dialogs.AskUnsavedChangesAsync(DocumentName ?? string.Empty);
        return choice switch
        {
            UnsavedChoice.Save => await SaveAsync(),
            UnsavedChoice.Discard => true,
            _ => false,
        };
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

    private async Task<PdfOpenResult> OpenInBackgroundAsync(string path, string? password)
    {
        var opening = Task.Run(() => _engine.Open(path, password));
        if (await Task.WhenAny(opening, Task.Delay(_progressDelay)) != opening)
        {
            State = ViewState.Loading;
        }
        return await opening;
    }

    private async Task OpenWithDialogAsync()
    {
        if (Dialogs is not null && await Dialogs.PickFileToOpenAsync() is { } path)
        {
            await OpenFileAsync(path);
        }
    }

    /// <summary>
    /// Save: a printed document is moved to a place the user chooses (with page changes, the edited
    /// version is written and the print file removed); an opened file is replaced by its edited version.
    /// </summary>
    private async Task<bool> SaveAsync()
    {
        if (!HasUnsavedChanges || _sourcePath is null || Dialogs is null)
        {
            return false;
        }
        if (_isPrinted)
        {
            var title = PrintFileName.TitleOf(_sourcePath) ?? Texts.Get("Print.DefaultName");
            var target = await Dialogs.PickSaveTargetAsync(title + ".pdf", null);
            return target is not null && await SaveToAsync(target, removeSource: true);
        }
        return await SaveToAsync(_sourcePath, removeSource: false);
    }

    /// <summary>Saves an opened file under a new name or place; the original stays as it is.</summary>
    private async Task SaveAsAsync()
    {
        if (!CanSaveAs || _sourcePath is null || Dialogs is null)
        {
            return;
        }
        var target = await Dialogs.PickSaveTargetAsync(Path.GetFileName(_sourcePath), Path.GetDirectoryName(_sourcePath));
        if (target is not null)
        {
            await SaveToAsync(target, removeSource: false);
        }
    }

    private async Task<bool> SaveToAsync(string target, bool removeSource)
    {
        var source = _sourcePath!;
        var document = _document!;
        var edited = _hasChanges;
        var saved = await Task.Run(() => edited
            ? _saver.Save(document.Save, target, removeSource ? source : null)
            : _saver.Save(source, target, move: removeSource));
        if (!saved)
        {
            await Dialogs!.ShowSaveErrorAsync(Path.GetFileName(target));
            return false;
        }
        _sourcePath = target;
        _isPrinted = false;
        _hasChanges = false;
        DocumentName = Path.GetFileName(target);
        OnDocumentStateChanged();
        return true;
    }

    private void CloseDocument()
    {
        Overview.Clear();
        _document?.Dispose();
        _document = null;
        _sourcePath = null;
        _isPrinted = false;
        _hasChanges = false;
        DocumentName = null;
        State = ViewState.Empty;
    }

    private void OnDocumentStateChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
    }

    /// <summary>Runs a user action, one at a time; errors are logged, never thrown into the UI loop.</summary>
    internal async void Run(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User action failed");
        }
        finally
        {
            _busy = false;
        }
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
