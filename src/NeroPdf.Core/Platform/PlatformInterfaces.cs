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
}

/// <summary>Space the operating system needs inside the custom header (window buttons).</summary>
/// <param name="Left">Space on the left, e.g. for the macOS window buttons.</param>
/// <param name="Right">Space on the right, e.g. for the Windows caption buttons.</param>
public readonly record struct HeaderInsets(double Left, double Right);

/// <summary>Everything the shared app needs from the platform project.</summary>
public sealed record PlatformServices(IPlatformPaths Paths, IPrintInbox PrintInbox, HeaderInsets HeaderInsets);

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
