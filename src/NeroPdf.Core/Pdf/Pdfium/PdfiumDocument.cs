using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NeroPdf.Core.Pdf.Pdfium;

/// <summary>
/// A document opened with PDFium. It lives in memory: page operations change only this working copy,
/// the original file is untouched until the caller saves.
/// </summary>
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

    public int PageCount { get; private set; }

    public bool IsModified { get; private set; }

    public PdfPageSize GetPageSize(int index)
    {
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            CheckIndex(index);
            return PdfiumNative.GetPageSizeByIndexF(_handle, index, out var size) != 0
                ? new PdfPageSize(size.Width, size.Height)
                : new PdfPageSize(595, 842);
        }
    }

    public PdfBitmap RenderPage(int index, int width, int height)
    {
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
                CheckIndex(index);
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

    public void RotatePages(IReadOnlyCollection<int> indices, int quarterTurns)
    {
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            foreach (var index in indices.Distinct())
            {
                CheckIndex(index);
                var page = PdfiumNative.LoadPage(_handle, index);
                if (page == IntPtr.Zero)
                {
                    throw new InvalidOperationException("PDFium could not load the page.");
                }
                try
                {
                    var rotation = ((PdfiumNative.PageGetRotation(page) + quarterTurns) % 4 + 4) % 4;
                    PdfiumNative.PageSetRotation(page, rotation);
                }
                finally
                {
                    PdfiumNative.ClosePage(page);
                }
            }
            IsModified = true;
        }
    }

    public void DeletePages(IReadOnlyCollection<int> indices)
    {
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            var sorted = indices.Distinct().OrderByDescending(i => i).ToArray();
            foreach (var index in sorted)
            {
                CheckIndex(index);
            }
            if (sorted.Length >= PageCount)
            {
                throw new InvalidOperationException("At least one page must remain.");
            }
            foreach (var index in sorted)
            {
                PdfiumNative.PageDelete(_handle, index);
            }
            PageCount = PdfiumNative.GetPageCount(_handle);
            IsModified = true;
        }
    }

    public unsafe void MovePages(IReadOnlyCollection<int> indices, int targetIndex)
    {
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            var sorted = indices.Distinct().Order().ToArray();
            foreach (var index in sorted)
            {
                CheckIndex(index);
            }
            ArgumentOutOfRangeException.ThrowIfNegative(targetIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(targetIndex, PageCount - sorted.Length);
            fixed (int* pages = sorted)
            {
                if (PdfiumNative.MovePages(_handle, pages, (nuint)sorted.Length, targetIndex) == 0)
                {
                    throw new InvalidOperationException("PDFium could not move the pages.");
                }
            }
            IsModified = true;
        }
    }

    public unsafe void Save(Stream output)
    {
        lock (PdfiumEngine.Sync)
        {
            ThrowIfDisposed();
            var target = GCHandle.Alloc(output);
            try
            {
                var writer = new Writer
                {
                    Base = new PdfiumNative.FileWrite { Version = 1, WriteBlock = &WriteBlock },
                    Target = GCHandle.ToIntPtr(target),
                };
                if (PdfiumNative.SaveAsCopy(_handle, &writer.Base, PdfiumNative.SaveNoIncremental) == 0)
                {
                    throw new IOException("PDFium could not write the document.");
                }
            }
            finally
            {
                target.Free();
            }
        }
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

    // PDFium calls this for every block of the file; the stream is found through the struct that
    // starts with PDFium's FPDF_FILEWRITE.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int WriteBlock(PdfiumNative.FileWrite* self, byte* data, CULong size)
    {
        try
        {
            var stream = (Stream)GCHandle.FromIntPtr(((Writer*)self)->Target).Target!;
            stream.Write(new ReadOnlySpan<byte>(data, checked((int)size.Value)));
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    private void CheckIndex(int index) => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)PageCount, nameof(index));

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);

    [StructLayout(LayoutKind.Sequential)]
    private struct Writer
    {
        public PdfiumNative.FileWrite Base;
        public IntPtr Target;
    }
}
