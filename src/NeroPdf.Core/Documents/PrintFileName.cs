using System.Text;

namespace NeroPdf.Core.Documents;

/// <summary>
/// File names in the print inbox carry the print job title, so Save can suggest a sensible name:
/// "&lt;title&gt;~&lt;unique id&gt;.pdf". The unique id keeps parallel jobs apart.
/// </summary>
public static class PrintFileName
{
    private const char Separator = '~';
    private const int MaxTitleLength = 80;

    // Prefixes that programs put in front of the document name in the job title.
    private static readonly string[] JobTitleSeparators = [" - ", " – "];

    /// <summary>Creates the inbox file name (without extension) for a print job.</summary>
    public static string Create(string? jobTitle, DateTime time, Guid id)
    {
        var title = Clean(jobTitle);
        var unique = time.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + "-" + id.ToString("N")[..8];
        return title.Length > 0 ? title + Separator + unique : unique;
    }

    /// <summary>Returns the document title from an inbox file name, or null if there is none.</summary>
    public static string? TitleOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var index = name.LastIndexOf(Separator);
        return index > 0 ? name[..index] : null;
    }

    /// <summary>
    /// Turns a print job title like "Microsoft Word - Letter.docx" into "Letter":
    /// application prefix and file extension removed, characters invalid in file names removed.
    /// </summary>
    public static string Clean(string? jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return string.Empty;
        }
        var title = jobTitle.Trim();
        foreach (var separator in JobTitleSeparators)
        {
            var index = title.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && index + separator.Length < title.Length)
            {
                title = title[(index + separator.Length)..];
                break;
            }
        }
        var extension = Path.GetExtension(title);
        if (extension.Length is > 1 and <= 5 && !extension.Contains(' '))
        {
            title = title[..^extension.Length];
        }

        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*', Separator]).ToHashSet();
        var builder = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (!invalid.Contains(c) && !char.IsControl(c))
            {
                builder.Append(c);
            }
        }
        var cleaned = builder.ToString().Trim().TrimEnd('.');
        return cleaned.Length > MaxTitleLength ? cleaned[..MaxTitleLength].Trim() : cleaned;
    }
}
