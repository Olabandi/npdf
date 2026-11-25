using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Documents;

/// <summary>
/// Writes documents safely: the content first goes into a temporary file next to the target, which
/// then replaces the target in one step. The target is never left half-written, and saving under
/// the same name at the same place works because the open document does not lock its file.
/// </summary>
public sealed class DocumentSaver(ILogger<DocumentSaver> logger)
{
    private const string TemporaryExtension = ".nero-tmp";

    /// <summary>Copies the file <paramref name="source"/> to <paramref name="target"/>.</summary>
    /// <param name="move">True for printed documents: the source is removed after a successful save.</param>
    public bool Save(string source, string target, bool move) =>
        Save(output =>
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            input.CopyTo(output);
        }, target, move ? source : null);

    /// <summary>Writes new content (e.g. an edited document) to <paramref name="target"/>.</summary>
    /// <param name="sourceToRemove">For printed documents: the temporary print file, removed after a successful save.</param>
    public bool Save(Action<Stream> write, string target, string? sourceToRemove)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(target))!;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N")[..8] + TemporaryExtension);
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(output);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
            logger.LogInformation("Document saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Document could not be saved");
            TryDelete(temporary);
            return false;
        }

        if (sourceToRemove is not null && !SamePath(sourceToRemove, target))
        {
            TryDelete(sourceToRemove);
        }
        return true;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "File could not be removed");
        }
    }
}
