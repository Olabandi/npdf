using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NeroPdf.App.ViewModels;
using NeroPdf.Core.Pdf;

namespace NeroPdf.App.Controls;

/// <summary>
/// Shows one PDF page. The page is rendered in the background only while the control is realized
/// by the virtualizing list, at the screen's pixel size, and released again when it scrolls away.
/// </summary>
public sealed class PdfPageView : Control
{
    public static readonly StyledProperty<PageViewModel?> PageProperty =
        AvaloniaProperty.Register<PdfPageView, PageViewModel?>(nameof(Page));

    public static readonly StyledProperty<double> MaxPageWidthProperty =
        AvaloniaProperty.Register<PdfPageView, double>(nameof(MaxPageWidth), 900);

    public static readonly StyledProperty<IBrush?> PageBackgroundProperty =
        AvaloniaProperty.Register<PdfPageView, IBrush?>(nameof(PageBackground), Brushes.White);

    private static readonly TimeSpan ResizeDelay = TimeSpan.FromMilliseconds(150);

    private readonly DispatcherTimer _resizeTimer;
    private CancellationTokenSource? _rendering;
    private WriteableBitmap? _bitmap;
    private PixelSize _requested;

    static PdfPageView()
    {
        AffectsMeasure<PdfPageView>(PageProperty, MaxPageWidthProperty);
        AffectsRender<PdfPageView>(PageBackgroundProperty);
    }

    public PdfPageView()
    {
        _resizeTimer = new DispatcherTimer { Interval = ResizeDelay };
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer.Stop();
            RequestRender();
        };
    }

    /// <summary>Raised when the page bitmap is drawn for the first time.</summary>
    public event EventHandler<int>? PageShown;

    public PageViewModel? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public double MaxPageWidth
    {
        get => GetValue(MaxPageWidthProperty);
        set => SetValue(MaxPageWidthProperty, value);
    }

    public IBrush? PageBackground
    {
        get => GetValue(PageBackgroundProperty);
        set => SetValue(PageBackgroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MaxPageWidth : Math.Min(availableSize.Width, MaxPageWidth);
        var aspect = Page?.AspectRatio ?? 1.414;
        return new Size(width, width * aspect);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty)
        {
            ReleaseBitmap();
            RequestRender();
        }
        else if (change.Property == BoundsProperty)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestRender();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _resizeTimer.Stop();
        ReleaseBitmap();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(PageBackground ?? Brushes.White, bounds);
        if (_bitmap is not null)
        {
            context.DrawImage(_bitmap, new Rect(_bitmap.Size), bounds);
        }
    }

    private void RequestRender()
    {
        var page = Page;
        var root = TopLevel.GetTopLevel(this);
        if (page is null || root is null || Bounds.Width < 1 || Bounds.Height < 1)
        {
            return;
        }

        var scale = root.RenderScaling;
        var size = new PixelSize(Math.Max(1, (int)Math.Round(Bounds.Width * scale)),
            Math.Max(1, (int)Math.Round(Bounds.Height * scale)));
        if (size == _requested && (_bitmap is not null || _rendering is not null))
        {
            return;
        }
        _requested = size;
        _rendering?.Cancel();
        var cancellation = _rendering = new CancellationTokenSource();
        _ = RenderAsync(page, size, cancellation);
    }

    private async Task RenderAsync(PageViewModel page, PixelSize size, CancellationTokenSource cancellation)
    {
        WriteableBitmap bitmap;
        try
        {
            var pixels = await page.RenderAsync(size.Width, size.Height, cancellation.Token);
            bitmap = await Task.Run(() => ToBitmap(pixels), cancellation.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        if (cancellation.IsCancellationRequested || !ReferenceEquals(page, Page))
        {
            bitmap.Dispose();
            return;
        }
        var first = _bitmap is null;
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _rendering = null;
        InvalidateVisual();
        if (first)
        {
            PageShown?.Invoke(this, page.Index);
        }
    }

    private void ReleaseBitmap()
    {
        _rendering?.Cancel();
        _rendering = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _requested = default;
    }

    private static WriteableBitmap ToBitmap(PdfBitmap pixels)
    {
        var bitmap = new WriteableBitmap(new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var target = bitmap.Lock();
        for (var row = 0; row < pixels.Height; row++)
        {
            Marshal.Copy(pixels.Pixels, row * pixels.Stride, target.Address + row * target.RowBytes, pixels.Width * 4);
        }
        return bitmap;
    }
}
