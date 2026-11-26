using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NeroPdf.Core.Pdf.Pdfium;

/// <summary>Creates a PDF with one page per JPEG image (used for scans). Each page has the size of its image.</summary>
internal static class PdfiumImageDocument
{
    private const double PointsPerInch = 72;

    public static void Write(IReadOnlyList<PdfImagePage> pages, Stream output)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is needed.", nameof(pages));
        }
        var images = pages.Select(p => File.ReadAllBytes(p.JpegPath)).ToArray();

        lock (PdfiumEngine.Sync)
        {
            var document = PdfiumNative.CreateNewDocument();
            if (document == IntPtr.Zero)
            {
                throw new InvalidOperationException("PDFium could not create a document.");
            }
            try
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    AddPage(document, i, images[i], pages[i].Dpi);
                }
                PdfiumDocument.SaveTo(document, output);
            }
            finally
            {
                PdfiumNative.CloseDocument(document);
            }
        }
    }

    private static unsafe void AddPage(IntPtr document, int index, byte[] jpeg, double dpi)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dpi, 0);
        var image = PdfiumNative.PageObjNewImageObj(document);
        if (image == IntPtr.Zero)
        {
            throw new InvalidOperationException("PDFium could not create an image object.");
        }
        var inserted = false;
        var data = GCHandle.Alloc(jpeg, GCHandleType.Pinned);
        try
        {
            var source = new Source { Data = (byte*)data.AddrOfPinnedObject(), Length = jpeg.Length };
            var access = new PdfiumNative.FileAccess
            {
                FileLength = new CULong((nuint)jpeg.Length),
                GetBlock = &GetBlock,
                Param = (IntPtr)(&source),
            };
            // "Inline" reads the image right away, so the pinned data is only needed during this call.
            if (PdfiumNative.ImageObjLoadJpegFileInline(null, 0, image, &access) == 0
                || PdfiumNative.ImageObjGetImagePixelSize(image, out var pixelWidth, out var pixelHeight) == 0
                || pixelWidth == 0 || pixelHeight == 0)
            {
                throw new InvalidDataException("The scanned image could not be read.");
            }

            var width = pixelWidth * PointsPerInch / dpi;
            var height = pixelHeight * PointsPerInch / dpi;
            var page = PdfiumNative.PageNew(document, index, width, height);
            if (page == IntPtr.Zero)
            {
                throw new InvalidOperationException("PDFium could not create a page.");
            }
            try
            {
                // The image is drawn into the unit square; the matrix scales it to the whole page.
                PdfiumNative.ImageObjSetMatrix(image, width, 0, 0, height, 0, 0);
                PdfiumNative.PageInsertObject(page, image);
                inserted = true;
                if (PdfiumNative.PageGenerateContent(page) == 0)
                {
                    throw new InvalidOperationException("PDFium could not write the page content.");
                }
            }
            finally
            {
                PdfiumNative.ClosePage(page);
            }
        }
        finally
        {
            data.Free();
            if (!inserted)
            {
                PdfiumNative.PageObjDestroy(image);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int GetBlock(IntPtr param, CULong position, byte* buffer, CULong size)
    {
        var source = (Source*)param;
        var start = (long)position.Value;
        var count = (long)size.Value;
        if (start < 0 || count < 0 || start + count > source->Length)
        {
            return 0;
        }
        new ReadOnlySpan<byte>(source->Data + start, (int)count).CopyTo(new Span<byte>(buffer, (int)count));
        return 1;
    }

    private unsafe struct Source
    {
        public byte* Data;
        public long Length;
    }
}
