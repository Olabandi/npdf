using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Pdf;

namespace NeroPdf.App.ViewModels;

/// <summary>One page of the open document. Rendering happens on demand in the background.</summary>
public sealed class PageViewModel(IPdfDocument document, int index, PdfPageSize size) : ObservableObject
{
    private bool _isSelected;
    private bool _isCurrent;
    private bool _isToolbarAnchor;
    private bool _isDragged;
    private bool _hasGapBefore;

    public int Index { get; } = index;

    /// <summary>Page number as shown to the user.</summary>
    public int Number => Index + 1;

    /// <summary>Height divided by width, as displayed.</summary>
    public double AspectRatio { get; } = size.Width > 0 ? size.Height / size.Width : 1.414;

    /// <summary>Marked in the page overview; page operations work on all marked pages.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>The page shown in the main view.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => Set(ref _isCurrent, value);
    }

    /// <summary>The small toolbar is shown next to this page (the last marked one).</summary>
    public bool IsToolbarAnchor
    {
        get => _isToolbarAnchor;
        set => Set(ref _isToolbarAnchor, value);
    }

    /// <summary>Being dragged; shown dimmed in the list.</summary>
    public bool IsDragged
    {
        get => _isDragged;
        set => Set(ref _isDragged, value);
    }

    /// <summary>A gap opens before this page while pages are dragged to this place.</summary>
    public bool HasGapBefore
    {
        get => _hasGapBefore;
        set => Set(ref _hasGapBefore, value);
    }

    public Task<PdfBitmap> RenderAsync(int width, int height, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return document.RenderPage(Index, width, height);
        }, cancellationToken);
}
