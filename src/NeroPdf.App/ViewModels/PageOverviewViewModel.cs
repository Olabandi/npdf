using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Pdf;

namespace NeroPdf.App.ViewModels;

/// <summary>
/// The pages of the open document: current page and navigation, marking pages, and the page
/// operations (rotate, delete, reorder) on the marked pages.
/// </summary>
public sealed class PageOverviewViewModel : ObservableObject
{
    private readonly ILogger<PageOverviewViewModel> _logger;
    private IPdfDocument? _document;
    private int _currentPage = 1;
    private int _anchor = -1;
    private bool _busy;
    private int[] _dragged = [];
    private int _dropIndex = -1;

    public PageOverviewViewModel(ILogger<PageOverviewViewModel> logger)
    {
        _logger = logger;
        RotateLeftCommand = new RelayCommand(() => Run(() => RotateAsync(-1)));
        RotateRightCommand = new RelayCommand(() => Run(() => RotateAsync(1)));
        DeleteCommand = new RelayCommand(() => Run(DeleteAsync));
        MoveUpCommand = new RelayCommand(() => Run(() => MoveByAsync(-1)));
        MoveDownCommand = new RelayCommand(() => Run(() => MoveByAsync(1)));
    }

    /// <summary>Raised after every page operation; the document now differs from its file.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Raised when the view should scroll so that the page with this index is at the top.</summary>
    public event EventHandler<int>? ScrollToPageRequested;

    public ObservableCollection<PageViewModel> Pages { get; } = [];

    public ICommand RotateLeftCommand { get; }

    public ICommand RotateRightCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (Set(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageIndicator));
                UpdateCurrentMarks();
            }
        }
    }

    public string PageIndicator => Texts.Format("Viewer.PageIndicator", CurrentPage, Pages.Count);

    public string PageCountText => Texts.Format("Sidebar.PageCount", Pages.Count);

    public int SelectedCount => Pages.Count(p => p.IsSelected);

    public string SelectionText => SelectedCount == 1
        ? Texts.Get("Pages.SelectedOne")
        : Texts.Format("Pages.SelectedMany", SelectedCount);

    /// <summary>Deleting is possible as long as at least one page remains.</summary>
    public bool CanDelete => SelectedCount > 0 && SelectedCount < Pages.Count;

    public void Load(IPdfDocument document)
    {
        _document = document;
        Rebuild(selected: [], current: 0);
    }

    public void Clear()
    {
        _document = null;
        Pages.Clear();
        _anchor = -1;
        NotifyCounts();
    }

    // ---- Navigation -------------------------------------------------------------------------

    /// <summary>Jumps to a page (0-based index), clamped to the document.</summary>
    public void GoToPage(int index)
    {
        if (Pages.Count == 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, Pages.Count - 1);
        CurrentPage = index + 1;
        ScrollToPageRequested?.Invoke(this, index);
    }

    public void NextPage() => GoToPage(CurrentPage);

    public void PreviousPage() => GoToPage(CurrentPage - 2);

    public void FirstPage() => GoToPage(0);

    public void LastPage() => GoToPage(Pages.Count - 1);

    // ---- Marking pages ----------------------------------------------------------------------

    /// <summary>Plain click: mark only this page and show it.</summary>
    public void SelectOnly(int index)
    {
        foreach (var page in Pages)
        {
            page.IsSelected = page.Index == index;
        }
        SetAnchor(index);
        GoToPage(index);
    }

    /// <summary>Ctrl click or the check box: add or remove this page.</summary>
    public void Toggle(int index)
    {
        if (index < 0 || index >= Pages.Count)
        {
            return;
        }
        Pages[index].IsSelected = !Pages[index].IsSelected;
        SetAnchor(Pages[index].IsSelected ? index : LastSelectedIndex());
    }

    /// <summary>Shift click: mark the range from the last marked page to this one.</summary>
    public void SelectRange(int index)
    {
        var from = _anchor >= 0 ? _anchor : index;
        var (low, high) = from <= index ? (from, index) : (index, from);
        foreach (var page in Pages)
        {
            page.IsSelected = page.Index >= low && page.Index <= high;
        }
        SetAnchor(index);
    }

    /// <summary>Right click on a page that is not marked: mark only this one, without scrolling.</summary>
    public void EnsureSelected(int index)
    {
        if (index >= 0 && index < Pages.Count && !Pages[index].IsSelected)
        {
            foreach (var page in Pages)
            {
                page.IsSelected = page.Index == index;
            }
            SetAnchor(index);
        }
    }

    // ---- Drag and drop ----------------------------------------------------------------------

    /// <summary>Starts dragging the page under the pointer (all marked pages if it is marked).</summary>
    public int BeginDrag(int index)
    {
        if (!Pages[index].IsSelected)
        {
            EnsureSelected(index);
        }
        _dragged = SelectedIndices();
        foreach (var page in Pages)
        {
            page.IsDragged = page.IsSelected;
        }
        _dropIndex = -1;
        return _dragged.Length;
    }

    /// <summary>Opens the gap before the page with this index (Pages.Count = after the last page).</summary>
    public void UpdateDropIndex(int index)
    {
        if (index == _dropIndex)
        {
            return;
        }
        _dropIndex = index;
        foreach (var page in Pages)
        {
            page.HasGapBefore = page.Index == index;
        }
        OnPropertyChanged(nameof(HasGapAtEnd));
    }

    /// <summary>The gap is after the last page.</summary>
    public bool HasGapAtEnd => _dropIndex == Pages.Count && _dragged.Length > 0;

    public async Task EndDragAsync(bool drop)
    {
        var dragged = _dragged;
        var dropIndex = _dropIndex;
        _dragged = [];
        _dropIndex = -1;
        foreach (var page in Pages)
        {
            page.IsDragged = false;
            page.HasGapBefore = false;
        }
        OnPropertyChanged(nameof(HasGapAtEnd));
        if (drop && dropIndex >= 0 && dragged.Length > 0)
        {
            // The drop index counts the pages as they are now; the target counts them after the
            // dragged pages were taken out.
            var target = dropIndex - dragged.Count(i => i < dropIndex);
            await MoveToAsync(dragged, target);
        }
    }

    // ---- Page operations --------------------------------------------------------------------

    private async Task RotateAsync(int quarterTurns)
    {
        var selected = SelectedIndices();
        if (_document is null || selected.Length == 0)
        {
            return;
        }
        var document = _document;
        await Task.Run(() => document.RotatePages(selected, quarterTurns));
        _logger.LogInformation("Rotated {Count} pages by {Turns} quarter turns", selected.Length, quarterTurns);
        Rebuild(selected, CurrentPage - 1);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DeleteAsync()
    {
        var selected = SelectedIndices();
        if (_document is null || !CanDelete)
        {
            return;
        }
        var document = _document;
        await Task.Run(() => document.DeletePages(selected));
        _logger.LogInformation("Deleted {Count} pages", selected.Length);
        var first = Math.Min(selected[0], document.PageCount - 1);
        Rebuild([first], first);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        GoToPage(first);
    }

    private async Task MoveByAsync(int step)
    {
        var selected = SelectedIndices();
        if (selected.Length == 0)
        {
            return;
        }
        var target = Math.Clamp(selected[0] + step, 0, Pages.Count - selected.Length);
        if (target == selected[0] && IsContiguous(selected))
        {
            return;
        }
        await MoveToAsync(selected, target);
    }

    private async Task MoveToAsync(int[] pages, int target)
    {
        if (_document is null || pages.Length == 0)
        {
            return;
        }
        target = Math.Clamp(target, 0, Pages.Count - pages.Length);
        if (target == pages[0] && IsContiguous(pages))
        {
            return;
        }
        var document = _document;
        await Task.Run(() => document.MovePages(pages, target));
        _logger.LogInformation("Moved {Count} pages to position {Target}", pages.Length, target + 1);
        var moved = Enumerable.Range(target, pages.Length).ToArray();
        Rebuild(moved, target);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        GoToPage(target);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private void Rebuild(IReadOnlyCollection<int> selected, int current)
    {
        var document = _document;
        if (document is null)
        {
            return;
        }
        Pages.Clear();
        for (var i = 0; i < document.PageCount; i++)
        {
            Pages.Add(new PageViewModel(document, i, document.GetPageSize(i)) { IsSelected = selected.Contains(i) });
        }
        _anchor = -1;
        SetAnchor(selected.Count > 0 ? selected.Max() : -1);
        _currentPage = Math.Clamp(current, 0, Math.Max(0, Pages.Count - 1)) + 1;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(PageIndicator));
        UpdateCurrentMarks();
        NotifyCounts();
    }

    private void SetAnchor(int index)
    {
        _anchor = index;
        foreach (var page in Pages)
        {
            page.IsToolbarAnchor = page.Index == index && page.IsSelected;
        }
        NotifyCounts();
    }

    private void UpdateCurrentMarks()
    {
        foreach (var page in Pages)
        {
            page.IsCurrent = page.Index == CurrentPage - 1;
        }
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(PageIndicator));
    }

    private int[] SelectedIndices() => Pages.Where(p => p.IsSelected).Select(p => p.Index).ToArray();

    private int LastSelectedIndex() => Pages.LastOrDefault(p => p.IsSelected)?.Index ?? -1;

    private static bool IsContiguous(int[] sorted) => sorted[^1] - sorted[0] == sorted.Length - 1;

    /// <summary>Runs one page operation at a time; errors are logged, never thrown into the UI loop.</summary>
    private async void Run(Func<Task> action)
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
            _logger.LogError(ex, "Page operation failed");
        }
        finally
        {
            _busy = false;
        }
    }
}
