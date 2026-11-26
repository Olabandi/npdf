using NeroPdf.Core.Documents;

namespace NeroPdf.Core.Platform;

/// <summary>
/// Print inbox in a folder: the print task writes "*.part" files into Incoming and renames them to
/// "*.pdf" when they are complete. Claiming moves a file to Opened; the move succeeds for exactly
/// one Nero PDF instance, so parallel print jobs never end up in the same window.
/// Saving a printed document moves it out of these folders, so whatever stays here was never saved.
/// </summary>
public sealed class FolderPrintInbox(string tempDirectory) : IPrintInbox
{
    private string Incoming => Path.Combine(tempDirectory, PrintFolders.Incoming);

    private string Opened => Path.Combine(tempDirectory, PrintFolders.Opened);

    private string Conversions => Path.Combine(tempDirectory, PrintFolders.Conversions);

    /// <summary>How long a registered conversion waits for its printout.</summary>
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(5);

    public string? TryClaimNext()
    {
        if (!Directory.Exists(Incoming))
        {
            return null;
        }
        Directory.CreateDirectory(Opened);
        foreach (var file in new DirectoryInfo(Incoming).GetFiles("*.pdf").OrderBy(f => f.CreationTimeUtc))
        {
            var target = Path.Combine(Opened, file.Name);
            try
            {
                File.Move(file.FullName, target);
                return target;
            }
            catch (IOException)
            {
                // Claimed by another instance in the meantime, or still in use: try the next one.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return null;
    }

    public string CreateDocumentPath(string title)
    {
        Directory.CreateDirectory(Opened);
        return Path.Combine(Opened, PrintFileName.Create(title, DateTime.Now, Guid.NewGuid()) + ".pdf");
    }

    public void RegisterConversion(string originPath)
    {
        Directory.CreateDirectory(Conversions);
        File.WriteAllText(Path.Combine(Conversions, Guid.NewGuid().ToString("N") + ".txt"), originPath);
    }

    public string? TakeConversionOrigin(string printedPath)
    {
        var title = PrintFileName.TitleOf(printedPath);
        if (title is null || !Directory.Exists(Conversions))
        {
            return null;
        }
        var limit = DateTime.UtcNow - ConversionTimeout;
        foreach (var marker in new DirectoryInfo(Conversions).GetFiles("*.txt").OrderBy(f => f.CreationTimeUtc))
        {
            try
            {
                if (marker.CreationTimeUtc < limit)
                {
                    marker.Delete();
                    continue;
                }
                var origin = File.ReadAllText(marker.FullName);
                // The print job is named after the file ("Microsoft Word - Letter.docx" → "Letter").
                if (string.Equals(PrintFileName.Clean(Path.GetFileName(origin)), title, StringComparison.OrdinalIgnoreCase))
                {
                    marker.Delete();
                    return origin;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Taken by another window at the same moment.
            }
        }
        return null;
    }

    public void CleanUp(TimeSpan maxAge)
    {
        var limit = DateTime.UtcNow - maxAge;
        foreach (var folder in new[] { Incoming, Opened })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }
            foreach (var file in new DirectoryInfo(folder).GetFiles())
            {
                if (file.LastWriteTimeUtc >= limit)
                {
                    continue;
                }
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
