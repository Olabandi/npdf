using NeroPdf.Core.Pdf;

namespace NeroPdf.Core.Platform;

/// <summary>Printing the document shown on a real printer, through the system's print dialog.</summary>
public interface IDocumentPrinter
{
    /// <summary>False where printing is not available yet (macOS until the Mac build).</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Shows the system print dialog (printer, pages, copies) over the given window. Returns the job to
    /// print, or null if the user cancelled. Must be called on the user interface thread.
    /// </summary>
    IPrintJob? AskForPrintJob(nint ownerWindow, int pageCount);
}

/// <summary>A print job the user confirmed. Printing may run in the background.</summary>
public interface IPrintJob : IDisposable
{
    /// <summary>The pages to print (0-based), in order.</summary>
    IReadOnlyList<int> Pages { get; }

    /// <summary>Prints the pages; <paramref name="progress"/> receives the number of pages done.</summary>
    /// <exception cref="IOException">The printer reported an error.</exception>
    void Print(IPdfDocument document, string title, IProgress<int>? progress);
}

public sealed class UnsupportedDocumentPrinter : IDocumentPrinter
{
    public bool IsSupported => false;

    public IPrintJob? AskForPrintJob(nint ownerWindow, int pageCount) => null;
}
