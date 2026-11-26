using NeroPdf.Core.Documents;

namespace NeroPdf.Core.Platform;

/// <summary>
/// Used where there is no virtual printer (development build, macOS until its PDF service exists).
/// Documents Nero PDF creates itself go to the temp folder of the platform.
/// </summary>
public sealed class NoPrintInbox : IPrintInbox
{
    public string? TryClaimNext() => null;

    public void CleanUp(TimeSpan maxAge)
    {
    }

    public void RegisterConversion(string originPath)
    {
    }

    public string? TakeConversionOrigin(string printedPath) => null;

    public string CreateDocumentPath(string title)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NeroPdf");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, PrintFileName.Create(title, DateTime.Now, Guid.NewGuid()) + ".pdf");
    }
}
