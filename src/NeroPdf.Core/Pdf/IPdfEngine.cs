namespace NeroPdf.Core.Pdf;

/// <summary>The PDF engine behind its own interface. The user interface never calls PDFium directly.</summary>
public interface IPdfEngine
{
    /// <summary>Opens a document. The file is read into memory and not kept locked.</summary>
    PdfOpenResult Open(string path, string? password = null);

    /// <summary>Writes a new PDF with one page per JPEG image (scans); each page has the size of its image.</summary>
    void CreateFromImages(IReadOnlyList<PdfImagePage> pages, Stream output);
}

/// <summary>A JPEG image and its resolution, which gives the page size.</summary>
public sealed record PdfImagePage(string JpegPath, double Dpi);

/// <summary>An opened document. All methods are thread-safe.</summary>
public interface IPdfDocument : IDisposable
{
    int PageCount { get; }

    /// <summary>Page size in points (1/72 inch), as displayed (page rotation applied).</summary>
    PdfPageSize GetPageSize(int index);

    /// <summary>Renders a page into a BGRA bitmap of the given pixel size on a white background.</summary>
    PdfBitmap RenderPage(int index, int width, int height);

    /// <summary>
    /// True after a page operation. All operations change only this in-memory working copy;
    /// the file stays as it is until the document is saved.
    /// </summary>
    bool IsModified { get; }

    /// <summary>Rotates pages by quarter turns (positive = clockwise), continuing from their current rotation.</summary>
    void RotatePages(IReadOnlyCollection<int> indices, int quarterTurns);

    /// <summary>Deletes pages. At least one page always remains.</summary>
    void DeletePages(IReadOnlyCollection<int> indices);

    /// <summary>
    /// Moves pages so that they stand together, in their current order, starting at
    /// <paramref name="targetIndex"/> of the resulting document.
    /// </summary>
    void MovePages(IReadOnlyCollection<int> indices, int targetIndex);

    /// <summary>Writes the working copy as a complete PDF file (protection and form contents are kept).</summary>
    void Save(Stream output);
}

public enum PdfOpenStatus
{
    Ok,
    NotFound,
    Damaged,
    PasswordRequired,
    WrongPassword,
    Unsupported,
    Failed,
}

public sealed record PdfOpenResult(PdfOpenStatus Status, IPdfDocument? Document)
{
    public static PdfOpenResult Failure(PdfOpenStatus status) => new(status, null);
}

public readonly record struct PdfPageSize(double Width, double Height);

/// <summary>Pixels in BGRA order, 4 bytes per pixel, rows of <see cref="Stride"/> bytes.</summary>
public sealed record PdfBitmap(int Width, int Height, int Stride, byte[] Pixels);
