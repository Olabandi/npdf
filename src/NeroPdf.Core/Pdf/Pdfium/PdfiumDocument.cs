using System.Runtime.InteropServices;

namespace NeroPdf.Core.Pdf.Pdfium;

/// <summary>A document opened with PDFium.</summary>
internal sealed class PdfiumDocument : IPdfDocument
{
    private const uint White = 0xFFFFFFFF;

    private readonly PdfiumEngine.NativeBuffer _buffer;
    private IntPtr _handle;

    public PdfiumDocument(IntPtr handle, PdfiumEngine.NativeBuffer buffer)
    {
        _handle = handle;
        _buffer = buffer;
        PageCount = PdfiumNative.GetPageCount(handle);
    }

    public int PageCount { get; }

    public PdfPageSize GetPageSize(int index)
    {
        CheckIndex(index);
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            return PdfiumNative.GetPageSizeByIndexF(_handle, index, out var size) != 0
                ? new PdfPageSize(size.Width, size.Height)
                : new PdfPageSize(595, 842);
        }
    }

    public PdfBitmap RenderPage(int index, int width, int height)
    {
        CheckIndex(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var stride = width * 4;
        var pixels = new byte[stride * height];
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            lock (PdfiumEngine.Sync)
            {
                ThrowIfDisposed();
                var bitmap = PdfiumNative.BitmapCreateEx(width, height, PdfiumNative.BitmapFormatBgra,
                    pin.AddrOfPinnedObject(), stride);
                if (bitmap == IntPtr.Zero)
                {
                    throw new InvalidOperationException("PDFium could not create a bitmap.");
                }
                var page = PdfiumNative.LoadPage(_handle, index);
                try
                {
                    PdfiumNative.BitmapFillRect(bitmap, 0, 0, width, height, White);
                    if (page != IntPtr.Zero)
                    {
                        PdfiumNative.RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, PdfiumNative.RenderAnnotations);
                    }
                }
                finally
                {
                    if (page != IntPtr.Zero)
                    {
                        PdfiumNative.ClosePage(page);
                    }
                    PdfiumNative.BitmapDestroy(bitmap);
                }
            }
        }
        finally
        {
            pin.Free();
        }
        return new PdfBitmap(width, height, stride, pixels);
    }

    public void Dispose()
    {
        lock (PdfiumEngine.Sync)
        {
            if (_handle != IntPtr.Zero)
            {
                PdfiumNative.CloseDocument(_handle);
                _handle = IntPtr.Zero;
                _buffer.Dispose();
            }
        }
    }

    private void CheckIndex(int index) => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)PageCount, nameof(index));

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
}
