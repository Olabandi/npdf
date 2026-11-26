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
