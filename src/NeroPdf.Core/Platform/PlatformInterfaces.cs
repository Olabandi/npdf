namespace NeroPdf.Core.Platform;

/// <summary>Data and temp folders of the platform. Implemented per platform.</summary>
public interface IPlatformPaths
{
    /// <summary>Folder for settings and logs of the current user.</summary>
    string DataDirectory { get; }

    /// <summary>Folder for logs.</summary>
    string LogDirectory => Path.Combine(DataDirectory, "Logs");
}

/// <summary>
/// Hands over documents that the virtual printer produced. Each document is claimed by exactly
/// one Nero PDF instance, so parallel print jobs open in separate windows.
/// </summary>
public interface IPrintInbox
{
    /// <summary>Claims the oldest completely written printed document, or returns null.</summary>
    string? TryClaimNext();

    /// <summary>Removes printed documents that were never saved and are older than <paramref name="maxAge"/>.</summary>
    void CleanUp(TimeSpan maxAge);

    /// <summary>
    /// Path for a new document that Nero PDF creates itself (a scan). It is treated like a printed
    /// document: temporary until the user saves it, then moved.
    /// </summary>
    string CreateDocumentPath(string title);
}

/// <summary>Space the operating system needs inside the custom header (window buttons).</summary>
/// <param name="Left">Space on the left, e.g. for the macOS window buttons.</param>
/// <param name="Right">Space on the right, e.g. for the Windows caption buttons.</param>
public readonly record struct HeaderInsets(double Left, double Right);

/// <summary>Registration as the default app for PDF files. The user always confirms it in the system settings.</summary>
public interface IDefaultPdfApp
{
    /// <summary>False where the platform offers no way to check or change it (e.g. unpackaged development build).</summary>
    bool IsSupported { get; }

    bool IsDefault();

    /// <summary>Opens the system settings where the user can make Nero PDF the default PDF app.</summary>
    void OpenSettings();
}

/// <summary>Used where the platform cannot check or change the default app.</summary>
public sealed class UnsupportedDefaultPdfApp : IDefaultPdfApp
{
    public bool IsSupported => false;

    public bool IsDefault() => false;

    public void OpenSettings()
    {
    }
}

/// <summary>Everything the shared app needs from the platform project.</summary>
public sealed record PlatformServices(IPlatformPaths Paths, IPrintInbox PrintInbox, HeaderInsets HeaderInsets,
    IDefaultPdfApp DefaultPdfApp, INeroSuite NeroSuite, IFileConverter Converter, IScanner Scanner);

/// <summary>Sub folders of the temp folder used between the virtual printer and the app.</summary>
public static class PrintFolders
{
    /// <summary>Completely written print results wait here.</summary>
    public const string Incoming = "Incoming";

    /// <summary>Print results that a Nero PDF window has claimed.</summary>
    public const string Opened = "Opened";

    /// <summary>Extension of files that are still being written.</summary>
    public const string PartialExtension = ".part";
}
