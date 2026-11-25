namespace NeroPdf.Core.Pdf;

/// <summary>The PDF engine behind its own interface. The user interface never calls PDFium directly.</summary>
public interface IPdfEngine
{
    /// <summary>Opens a document. The file is read into memory and not kept locked.</summary>
    PdfOpenResult Open(string path, string? password = null);
}

/// <summary>An opened document. All methods are thread-safe.</summary>
public interface IPdfDocument : IDisposable
{
    int PageCount { get; }

    /// <summary>Page size in points (1/72 inch), as displayed (page rotation applied).</summary>
    PdfPageSize GetPageSize(int index);

    /// <summary>Renders a page into a BGRA bitmap of the given pixel size on a white background.</summary>
    PdfBitmap RenderPage(int index, int width, int height);
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
