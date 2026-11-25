namespace NeroPdf.Core.Platform;

/// <summary>
/// Print inbox in a folder: the print task writes "*.part" files into Incoming and renames them to
/// "*.pdf" when they are complete. Claiming moves a file to Opened; the move succeeds for exactly
/// one Nero PDF instance, so parallel print jobs never end up in the same window.
/// </summary>
public sealed class FolderPrintInbox(string tempDirectory) : IPrintInbox
{
    public string? TryClaimNext()
    {
        var incoming = Path.Combine(tempDirectory, PrintFolders.Incoming);
        if (!Directory.Exists(incoming))
        {
            return null;
        }
        var opened = Path.Combine(tempDirectory, PrintFolders.Opened);
        Directory.CreateDirectory(opened);
        foreach (var file in new DirectoryInfo(incoming).GetFiles("*.pdf").OrderBy(f => f.CreationTimeUtc))
        {
            var target = Path.Combine(opened, file.Name);
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
}
