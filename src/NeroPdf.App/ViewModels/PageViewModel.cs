using NeroPdf.Core.Pdf;

namespace NeroPdf.App.ViewModels;

/// <summary>One page of the open document. Rendering happens on demand in the background.</summary>
public sealed class PageViewModel(IPdfDocument document, int index, PdfPageSize size)
{
    public int Index { get; } = index;

    /// <summary>Height divided by width, as displayed.</summary>
    public double AspectRatio { get; } = size.Width > 0 ? size.Height / size.Width : 1.414;

    public Task<PdfBitmap> RenderAsync(int width, int height, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return document.RenderPage(Index, width, height);
        }, cancellationToken);
}
