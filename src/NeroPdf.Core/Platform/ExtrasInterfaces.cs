namespace NeroPdf.Core.Platform;

/// <summary>
/// Scanning and converting are unlocked when a Nero suite (Nero Start / Nero Platinum) is installed.
/// Deliberately simple check for now.
/// </summary>
public interface INeroSuite
{
    bool IsInstalled();
}

/// <summary>
/// Converts other files to PDF: images directly (one page per image), everything else by printing it
/// on the Nero PDF printer.
/// </summary>
public interface IFileConverter
{
    /// <summary>False where the platform cannot do it (no virtual printer, e.g. development build or macOS).</summary>
    bool IsSupported { get; }

    /// <summary>The file types that can be converted. May take a moment (reads the system's file associations).</summary>
    ConvertibleTypes GetConvertibleTypes();

    /// <summary>
    /// Images: written as JPEG pages into <paramref name="folder"/>; the app makes the PDF.
    /// Other files: handed to the program registered for their type, which prints them on the Nero PDF
    /// printer; the result arrives like any other print job (this does not wait for it).
    /// </summary>
    ConvertOutcome Convert(string path, string folder);
}

/// <summary>Extensions (without dot, lower case) of convertible files: images and other documents.</summary>
public sealed record ConvertibleTypes(IReadOnlyList<string> Images, IReadOnlyList<string> Documents)
{
    public static ConvertibleTypes None { get; } = new([], []);

    public IEnumerable<string> All => Images.Concat(Documents);

    public bool Contains(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return extension.Length > 0 && All.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

public enum ConvertResult
{
    /// <summary>Sent to the printer; the result opens in its own window.</summary>
    Printing,

    /// <summary>The pages are ready as images.</summary>
    Pages,
    NoPrinter,
    NotConvertible,
    Failed,
}

public sealed record ConvertOutcome(ConvertResult Result, IReadOnlyList<ScannedImage>? Pages = null);

/// <summary>Scanners of the system. Calls may block; the app runs them in the background.</summary>
public interface IScanner
{
    IReadOnlyList<ScannerDevice> GetDevices();

    /// <summary>
    /// Scans from the document feeder all sheets, from the flatbed one page, and writes one JPEG file
    /// per page into <paramref name="folder"/>.
    /// </summary>
    /// <param name="progress">Receives the number of pages scanned so far, where the scanner reports it.</param>
    /// <exception cref="ScanException">The scanner reported an error.</exception>
    IReadOnlyList<ScannedImage> Scan(ScannerDevice device, ScanSettings settings, string folder, IProgress<int>? progress = null);
}

public sealed record ScannerDevice(string Id, string Name, bool HasFeeder, bool HasFlatbed);

public enum ScanSource
{
    Flatbed,
    Feeder,
}

public enum ScanColor
{
    Color,
    Gray,
}

public sealed record ScanSettings(ScanSource Source, ScanColor Color, int Dpi);

/// <summary>A page as JPEG image (from the scanner or a converted image file) and its resolution.</summary>
public sealed record ScannedImage(string JpegPath, int Dpi);

public enum ScanError
{
    Failed,
    NoPaper,
    Offline,

    /// <summary>The scanner stayed busy or stopped responding; the user can try again.</summary>
    Busy,
}

public sealed class ScanException(ScanError error, string message, Exception? inner = null,
    IReadOnlyList<ScannedImage>? pages = null) : Exception(message, inner)
{
    public ScanError Error { get; } = error;

    /// <summary>Pages scanned before the error (e.g. the first sheets of the feeder); they are kept.</summary>
    public IReadOnlyList<ScannedImage> Pages { get; } = pages ?? [];
}

/// <summary>Used where scanning and converting are not available (e.g. macOS for now).</summary>
public sealed class UnsupportedExtras : INeroSuite, IFileConverter, IScanner
{
    public bool IsSupported => false;

    public bool IsInstalled() => false;

    public ConvertibleTypes GetConvertibleTypes() => ConvertibleTypes.None;

    public ConvertOutcome Convert(string path, string folder) => new(ConvertResult.NoPrinter);

    public IReadOnlyList<ScannerDevice> GetDevices() => [];

    public IReadOnlyList<ScannedImage> Scan(ScannerDevice device, ScanSettings settings, string folder, IProgress<int>? progress = null) =>
        throw new ScanException(ScanError.Offline, "Scanning is not available on this platform.");
}
